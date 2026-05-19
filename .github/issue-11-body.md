## Sub-Issue 11: Delta Update Engine

Implement a binary delta engine that enables efficient incremental updates
by transmitting only the changed bytes between versions.

### Design

Vela Delta is a standalone crate (`vela-delta`) in the vela-core workspace:

```
vela-delta/
├── src/
│   ├── lib.rs
│   ├── diff.rs
│   ├── patch.rs
│   └── manifest.rs
└── Cargo.toml
```

### API

- `generate_delta(old: &[u8], new: &[u8]) -> DeltaResult<Vec<u8>>`
- `apply_patch(base: &[u8], patch: &[u8]) -> DeltaResult<Vec<u8>>`
- `DeltaManifest` — metadata for delta bundles

### Integration with FlashPack

- Uses `PayloadType::Delta` when building delta FlashPacks
- Includes `delta.manifest` in the .fpk tar archive
- Version baseline validation via `requires_version` in FpkHeader
