//! Patch applier — reconstructs target file from base + delta.

use tracing::{debug, error, info, instrument, trace};

use crate::{hash, DeltaError, DeltaResult, Instruction, DELTA_MAGIC};

/// Apply a delta patch to base data, producing the target.
///
/// The delta file format:
/// ```text
/// [magic: 4 bytes "VDLT"]
/// [base_hash: 32 bytes]
/// [target_hash: 32 bytes]
/// [instruction_count: 4 bytes LE u32]
/// [instructions...]
/// ```
#[instrument(skip(base, delta), fields(base_len = base.len(), delta_len = delta.len()))]
pub fn apply_patch(base: &[u8], delta: &[u8]) -> DeltaResult<Vec<u8>> {
    // Validate magic
    if delta.len() < 68 {
        return Err(DeltaError::InvalidFormat(
            "delta file too small — missing header".into(),
        ));
    }
    if &delta[..4] != DELTA_MAGIC {
        return Err(DeltaError::InvalidFormat(
            "invalid delta magic bytes".into(),
        ));
    }

    // Read hashes
    let expected_base_hash = hex::encode(&delta[4..36]);
    let expected_target_hash = hex::encode(&delta[36..68]);

    // Verify base hash
    let actual_base_hash = hash(base);
    if actual_base_hash != expected_base_hash {
        error!(
            expected = %expected_base_hash,
            actual = %actual_base_hash,
            "Base hash mismatch"
        );
        return Err(DeltaError::BaseHashMismatch {
            expected: expected_base_hash,
            actual: actual_base_hash,
        });
    }

    // Read instruction count
    let count = u32::from_le_bytes([
        delta[68], delta[69], delta[70], delta[71],
    ]) as usize;

    debug!(count, "Reading delta instructions");

    // Parse instructions
    let instructions = parse_instructions(&delta[72..], count)?;

    // Apply instructions to reconstruct target
    let target = apply_instructions(base, &instructions)?;

    // Verify target hash
    let actual_target_hash = hash(&target);
    if actual_target_hash != expected_target_hash {
        error!(
            expected = %expected_target_hash,
            actual = %actual_target_hash,
            "Target hash mismatch after patching"
        );
        return Err(DeltaError::TargetHashMismatch {
            expected: expected_target_hash,
            actual: actual_target_hash,
        });
    }

    info!(
        base_len = base.len(),
        target_len = target.len(),
        delta_len = delta.len(),
        "Patch applied successfully"
    );

    Ok(target)
}

/// Parse instructions from the delta body.
fn parse_instructions(data: &[u8], count: usize) -> DeltaResult<Vec<Instruction>> {
    let mut instructions = Vec::with_capacity(count);
    let mut pos = 0;

    for i in 0..count {
        let instr = Instruction::read_from(data, &mut pos).map_err(|e| {
            DeltaError::InvalidFormat(format!("instruction {i}: {e}"))
        })?;
        trace!(index = i, ?instr, "Parsed instruction");
        instructions.push(instr);
    }

    if pos != data.len() {
        debug!(
            parsed = pos,
            total = data.len(),
            "Extra bytes after instructions (ignored)"
        );
    }

    Ok(instructions)
}

/// Apply instructions to reconstruct the target from base.
fn apply_instructions(base: &[u8], instructions: &[Instruction]) -> DeltaResult<Vec<u8>> {
    // Estimate target size from COPY+INSERT lengths
    let target_size: usize = instructions
        .iter()
        .map(|i| match i {
            Instruction::Copy { length, .. } => *length as usize,
            Instruction::Insert { length, .. } => *length as usize,
        })
        .sum();

    let mut target = Vec::with_capacity(target_size);

    for (i, instr) in instructions.iter().enumerate() {
        match instr {
            Instruction::Copy { offset, length } => {
                let start = *offset as usize;
                let end = start + *length as usize;
                if end > base.len() {
                    return Err(DeltaError::InvalidFormat(format!(
                        "COPY instruction {i}: offset {start} + length {length} exceeds base size {}",
                        base.len()
                    )));
                }
                debug!(
                    instruction = i,
                    offset = start,
                    length = length,
                    "COPY from base"
                );
                target.extend_from_slice(&base[start..end]);
            }
            Instruction::Insert { data, .. } => {
                debug!(instruction = i, len = data.len(), "INSERT");
                target.extend_from_slice(data);
            }
        }
    }

    Ok(target)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::diff::generate_delta;

    #[test]
    fn test_patch_invalid_magic() {
        let result = apply_patch(b"base", b"XXXX...");
        assert!(result.is_err());
    }

    #[test]
    fn test_patch_too_small() {
        let result = apply_patch(b"base", b"VDLT");
        assert!(result.is_err());
    }

    #[test]
    fn test_patch_base_hash_mismatch() {
        let old = b"original file".to_vec();
        let new = b"modified file".to_vec();
        let delta = generate_delta(&old, &new).unwrap();

        // Apply with wrong base
        let result = apply_patch(b"wrong base data", &delta);
        assert!(matches!(
            result.unwrap_err(),
            DeltaError::BaseHashMismatch { .. }
        ));
    }

    #[test]
    fn test_patch_roundtrip_identical() {
        let data = b"VELA OTA FIRMWARE v1.0.0\x00\xFF".repeat(50);
        let delta = generate_delta(&data, &data).unwrap();
        let result = apply_patch(&data, &delta).unwrap();
        assert_eq!(result, data);
    }

    #[test]
    fn test_patch_roundtrip_partial_change() {
        let base = b"The quick brown fox jumps over the lazy dog".to_vec();
        let mut target = base.clone();
        target[10..15].copy_from_slice(b"BLACK");
        target[20..25].copy_from_slice(b"LEAPS");

        let delta = generate_delta(&base, &target).unwrap();
        let result = apply_patch(&base, &delta).unwrap();
        assert_eq!(result, target);
    }

    #[test]
    fn test_patch_roundtrip_append() {
        let base = b"original firmware v1.0".to_vec();
        let mut target = base.clone();
        target.extend_from_slice(b" -- patched to v2.0 with new features");

        let delta = generate_delta(&base, &target).unwrap();
        let result = apply_patch(&base, &delta).unwrap();
        assert_eq!(result, target);
    }

    #[test]
    fn test_patch_roundtrip_empty_old() {
        let old = vec![];
        let new = b"brand new firmware".to_vec();
        let delta = generate_delta(&old, &new).unwrap();
        let result = apply_patch(&old, &delta).unwrap();
        assert_eq!(result, new);
    }

    #[test]
    fn test_patch_roundtrip_large_binary() {
        let base: Vec<u8> = (0..4096u16)
            .flat_map(|i| i.to_le_bytes())
            .collect();
        let mut target = base.clone();
        // Modify middle section
        for i in 1000..1500 {
            target[i] = target[i].wrapping_add(1);
        }
        // Append new data
        target.extend_from_slice(b"NEW DATA AT END OF FIRMWARE");

        let delta = generate_delta(&base, &target).unwrap();
        let ratio = delta.len() as f64 / target.len() as f64;
        assert!(
            ratio < 0.5,
            "Delta should be significantly smaller than target (ratio: {:.1}%)",
            ratio * 100.0
        );

        let result = apply_patch(&base, &delta).unwrap();
        assert_eq!(result, target);
    }
}
