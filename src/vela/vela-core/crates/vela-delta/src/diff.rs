//! Binary delta generator using sliding-window block matching.
//!
//! The algorithm scans the new version looking for blocks that
//! already exist in the old version. When a match is found,
//! a COPY instruction is emitted. Non-matching bytes become
//! INSERT instructions.

use tracing::{debug, info, instrument, trace};

use crate::{hash, DeltaError, DeltaResult, DELTA_MAGIC};

/// Instruction in a delta patch.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Instruction {
    Copy { offset: u64, length: u32 },
    Insert { length: u32, data: Vec<u8> },
}

impl Instruction {
    fn serialized_size(&self) -> usize {
        match self {
            Self::Copy { .. } => 13,
            Self::Insert { length, data } => 5 + *length as usize,
        }
    }

    fn write_to(&self, buf: &mut Vec<u8>) {
        match self {
            Self::Copy { offset, length } => {
                buf.push(0);
                buf.extend_from_slice(&offset.to_le_bytes());
                buf.extend_from_slice(&length.to_le_bytes());
            }
            Self::Insert { length, data } => {
                buf.push(1);
                buf.extend_from_slice(&length.to_le_bytes());
                buf.extend_from_slice(data);
            }
        }
    }

    pub(crate) fn read_from(data: &[u8], pos: &mut usize) -> DeltaResult<Self> {
        if *pos >= data.len() {
            return Err(DeltaError::InvalidFormat("unexpected end of delta".into()));
        }
        let tag = data[*pos];
        *pos += 1;
        match tag {
            0 => {
                if *pos + 12 > data.len() {
                    return Err(DeltaError::InvalidFormat("truncated COPY".into()));
                }
                let offset = u64::from_le_bytes(data[*pos..*pos + 8].try_into().unwrap());
                *pos += 8;
                let length = u32::from_le_bytes(data[*pos..*pos + 4].try_into().unwrap());
                *pos += 4;
                Ok(Self::Copy { offset, length })
            }
            1 => {
                if *pos + 4 > data.len() {
                    return Err(DeltaError::InvalidFormat("truncated INSERT length".into()));
                }
                let length = u32::from_le_bytes(data[*pos..*pos + 4].try_into().unwrap()) as usize;
                *pos += 4;
                if *pos + length > data.len() {
                    return Err(DeltaError::InvalidFormat("truncated INSERT data".into()));
                }
                let ins = data[*pos..*pos + length].to_vec();
                *pos += length;
                Ok(Self::Insert { length: length as u32, data: ins })
            }
            t => Err(DeltaError::InvalidFormat(format!("unknown tag: {t}"))),
        }
    }
}

/// Generate a binary delta patch from `old` to `new`.
#[instrument(skip(old, new), fields(old_len = old.len(), new_len = new.len()))]
pub fn generate_delta(old: &[u8], new: &[u8]) -> DeltaResult<Vec<u8>> {
    let instructions = if old.is_empty() {
        vec![Instruction::Insert { length: new.len() as u32, data: new.to_vec() }]
    } else if new.is_empty() {
        return Err(DeltaError::InvalidFormat("cannot generate delta for empty target".into()));
    } else {
        sliding_window_diff(old, new)
    };

    encode_delta(old, new, &instructions)
}

/// Sliding-window diff: find matching blocks in old and emit COPY/INSERT.
fn sliding_window_diff(old: &[u8], new: &[u8]) -> Vec<Instruction> {
    let mut instructions: Vec<Instruction> = Vec::new();
    let mut new_pos = 0usize;
    let mut pending_insert: Vec<u8> = Vec::new();

    info!(old_len = old.len(), new_len = new.len(), "Computing delta");

    while new_pos < new.len() {
        let best = find_best_match(old, new, new_pos);

        if best.len >= MIN_MATCH_LEN {
            // Flush pending insert
            if !pending_insert.is_empty() {
                instructions.push(Instruction::Insert {
                    length: pending_insert.len() as u32,
                    data: std::mem::take(&mut pending_insert),
                });
            }

            instructions.push(Instruction::Copy {
                offset: best.old_offset as u64,
                length: best.len as u32,
            });

            new_pos = best.new_start + best.len;
        } else {
            // No match — accumulate into pending insert
            pending_insert.push(new[new_pos]);
            new_pos += 1;
        }
    }

    // Flush final pending insert
    if !pending_insert.is_empty() {
        instructions.push(Instruction::Insert {
            length: pending_insert.len() as u32,
            data: pending_insert,
        });
    }

    debug!(count = instructions.len(), "Delta generation complete");
    instructions
}

struct BlockMatch {
    old_offset: usize,
    new_start: usize,
    len: usize,
}

fn find_best_match(old: &[u8], new: &[u8], new_pos: usize) -> BlockMatch {
    let remaining = new.len() - new_pos;
    if remaining < MIN_MATCH_LEN || old.is_empty() {
        return BlockMatch { old_offset: 0, new_start: new_pos, len: 0 };
    }

    // Use first 4 bytes as fingerprint
    let fp = u32::from_le_bytes(new[new_pos..new_pos + 4].try_into().unwrap());

    let mut best = BlockMatch { old_offset: 0, new_start: new_pos, len: 0 };

    let mut old_pos = 0;
    while old_pos + 4 <= old.len() {
        let old_fp = u32::from_le_bytes(old[old_pos..old_pos + 4].try_into().unwrap());
        if old_fp == fp {
            let ml = extend_match(old, old_pos, new, new_pos);
            if ml > best.len {
                best = BlockMatch { old_offset: old_pos, new_start: new_pos, len: ml };
                if ml >= remaining { break; }
            }
        }
        old_pos += 1;
    }

    if best.len >= MIN_MATCH_LEN {
        trace!(offset = best.old_offset, len = best.len, "Match");
    }
    best
}

fn extend_match(old: &[u8], o: usize, new: &[u8], n: usize) -> usize {
    let max = (old.len() - o).min(new.len() - n);
    let mut len = 0;
    while len < max && old[o + len] == new[n + len] { len += 1; }
    len
}

/// Encode delta: magic + hashes + instructions.
fn encode_delta(old: &[u8], new: &[u8], instructions: &[Instruction]) -> DeltaResult<Vec<u8>> {
    let base_hash = hex::decode(hash(old)).unwrap();
    let target_hash = hex::decode(hash(new)).unwrap();
    let count = instructions.len() as u32;
    let instr_size: usize = instructions.iter().map(|i| i.serialized_size()).sum();
    let mut buf = Vec::with_capacity(4 + 32 + 32 + 4 + instr_size);

    buf.extend_from_slice(DELTA_MAGIC);
    buf.extend_from_slice(&base_hash);
    buf.extend_from_slice(&target_hash);
    buf.extend_from_slice(&count.to_le_bytes());
    for instr in instructions { instr.write_to(&mut buf); }

    info!(old = old.len(), new = new.len(), delta = buf.len(),
        ratio = format!("{:.1}", buf.len() as f64 / new.len() as f64 * 100.0),
        "Delta generated");

    Ok(buf)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_delta_identical() {
        let data = b"firmware v1.0 ".repeat(100);
        let delta = generate_delta(&data, &data).unwrap();
        assert!(delta.len() < 200, "delta={}", delta.len());
    }

    #[test]
    fn test_delta_completely_different() {
        let delta = generate_delta(&[0u8; 1024], &[1u8; 1024]).unwrap();
        assert!(!delta.is_empty());
    }

    #[test]
    fn test_delta_partial_change() {
        let old = b"AAAAAAAABBBBBBBBCCCCCCCCDDDDDDDD".to_vec();
        let mut new = old.clone();
        new[8..16].copy_from_slice(b"XXXXXXXX");
        let delta = generate_delta(&old, &new).unwrap();
        assert!(delta.len() < 200);
    }

    #[test]
    fn test_delta_empty_old() {
        let delta = generate_delta(&[], b"new file").unwrap();
        assert!(!delta.is_empty());
    }

    #[test]
    fn test_delta_magic() {
        let delta = generate_delta(b"old", b"new").unwrap();
        assert_eq!(&delta[..4], DELTA_MAGIC);
    }

    #[test]
    fn test_delta_hashes() {
        let old = b"old data";
        let new = b"new data";
        let delta = generate_delta(old, new).unwrap();
        assert_eq!(hex::encode(&delta[4..36]), hash(old));
        assert_eq!(hex::encode(&delta[36..68]), hash(new));
    }

    #[test]
    fn test_instruction_roundtrip() {
        let instrs = vec![
            Instruction::Copy { offset: 100, length: 50 },
            Instruction::Insert { length: 3, data: vec![1, 2, 3] },
        ];
        let mut buf = Vec::new();
        for i in &instrs { i.write_to(&mut buf); }
        let mut pos = 0;
        let decoded: Vec<_> = (0..2).map(|_| Instruction::read_from(&buf, &mut pos).unwrap()).collect();
        assert_eq!(instrs, decoded);
    }
}
