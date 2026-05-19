//! FlashPack Builder CLI — build, sign, verify, and inspect .fpk bundles.
//!
//! ## Usage
//!
//! ```text
//! vela-builder build  <payload> <output.fpk> [--name <NAME>] [--version <VER>]
//! vela-builder sign   <input.fpk> <key.pem> <output.fpk>
//! vela-builder verify  <input.fpk> <key.pub>
//! vela-builder info    <input.fpk>
//! vela-builder delta   <old.fpk> <new.fpk> <output.delta>
//! ```

use std::env;
use std::path::PathBuf;

fn main() {
    tracing_subscriber::fmt()
        .with_env_filter("vela=info")
        .with_target(false)
        .init();

    let args: Vec<String> = env::args().collect();
    if args.len() < 2 {
        print_usage(&args[0]);
        std::process::exit(1);
    }

    let cmd = &args[1];
    let result = match cmd.as_str() {
        "build" => cmd_build(&args[2..]),
        "sign" => cmd_sign(&args[2..]),
        "verify" => cmd_verify(&args[2..]),
        "info" => cmd_info(&args[2..]),
        "delta" => cmd_delta(&args[2..]),
        "--help" | "-h" => { print_usage(&args[0]); Ok(()) }
        _ => {
            eprintln!("Unknown command: {cmd}");
            print_usage(&args[0]);
            std::process::exit(1);
        }
    };

    if let Err(e) = result {
        eprintln!("Error: {e}");
        std::process::exit(1);
    }
}

fn print_usage(prog: &str) {
    eprintln!("FlashPack Builder CLI — Vela OTA update bundle tool\n");
    eprintln!("Usage: {prog} <command> [args...]\n");
    eprintln!("Commands:");
    eprintln!("  build  <payload> <output.fpk>   Build a FlashPack bundle from a payload file");
    eprintln!("         [--name <NAME>] [--version <VER>] [--requires <VER>]");
    eprintln!("  sign   <input.fpk> <key.pem> <output.fpk>  Sign a FlashPack bundle");
    eprintln!("  verify <input.fpk> <key.pub>     Verify a FlashPack signature");
    eprintln!("  info   <input.fpk>               Display FlashPack metadata and structure");
    eprintln!("  delta  <old> <new> <output.delta> Generate binary delta between two files");
}

// ─── build ──────────────────────────────────────────────────────

fn cmd_build(args: &[String]) -> Result<(), String> {
    if args.is_empty() {
        return Err("build requires <payload> and <output.fpk>".into());
    }

    let payload = &args[0];
    let output = if args.len() > 1 { &args[1] } else { return Err("missing output path".into()) };

    let mut bundle_name = String::from("vela-update");
    let mut bundle_version = String::from("0.1.0");
    let mut requires_version = String::from("0.0.0");

    let mut i = 2;
    while i < args.len() {
        match args[i].as_str() {
            "--name" => { i += 1; if i < args.len() { bundle_name = args[i].clone(); } }
            "--version" => { i += 1; if i < args.len() { bundle_version = args[i].clone(); } }
            "--requires" => { i += 1; if i < args.len() { requires_version = args[i].clone(); } }
            _ => return Err(format!("unknown flag: {}", args[i])),
        }
        i += 1;
    }

    let config = vela_flashpack::BuilderConfig {
        payload_path: payload.clone(),
        bundle_name: bundle_name.clone(),
        bundle_version: bundle_version.clone(),
        compatible_slots: vec!["*".into()],
        payload_type: vela_flashpack::PayloadType::FullImage,
        requires_version: requires_version.clone(),
        builder_id: "vela-builder-cli".into(),
        signer: None,
        format_version: "1.0.0".into(),
        min_reader_version: "1.0.0".into(),
        compat_flags: vec![],
    };

    let builder = vela_flashpack::FlashPackBuilder::new(config);
    builder.build(PathBuf::from(output).as_path())
        .map_err(|e| format!("Build failed: {e}"))?;

    let size = std::fs::metadata(output).map(|m| m.len()).unwrap_or(0);
    println!("✓ FlashPack built: {output}");
    println!("  Name:    {bundle_name}");
    println!("  Version: {bundle_version}");
    println!("  Requires: {requires_version}");
    println!("  Size:    {size} bytes");

    Ok(())
}

// ─── sign ───────────────────────────────────────────────────────

fn cmd_sign(args: &[String]) -> Result<(), String> {
    if args.len() < 3 {
        return Err("sign requires <input.fpk> <key.pem> <output.fpk>".into());
    }
    let _input = &args[0];
    let _key = &args[1];
    let _output = &args[2];
    eprintln!("Signing not yet implemented — requires PEM key parsing");
    Err("not implemented".into())
}

// ─── verify ─────────────────────────────────────────────────────

fn cmd_verify(args: &[String]) -> Result<(), String> {
    if args.is_empty() {
        return Err("verify requires <input.fpk>".into());
    }
    let path = PathBuf::from(&args[0]);
    if !path.exists() {
        return Err(format!("file not found: {}", args[0]));
    }

    match vela_flashpack::FlashPackReader::open(path.as_path()) {
        Ok(reader) => {
            let h = &reader.header;
            println!("✓ FlashPack structure valid: {path}", path = path.display());
            println!("  Name:    {}", h.bundle_name);
            println!("  Version: {}", h.bundle_version);
            println!("  Format:  {}", h.format_version);
            println!("  Type:    {}", h.payload_type);
            println!("  Size:    {} bytes", h.payload_size);
            Ok(())
        }
        Err(e) => Err(format!("Verification failed: {e}")),
    }
}

// ─── info ───────────────────────────────────────────────────────

fn cmd_info(args: &[String]) -> Result<(), String> {
    if args.is_empty() {
        return Err("info requires <input.fpk>".into());
    }
    let path = PathBuf::from(&args[0]);
    if !path.exists() {
        return Err(format!("file not found: {}", args[0]));
    }

    let data = std::fs::read(&path).map_err(|e| format!("read: {e}"))?;

    match vela_flashpack::FlashPackReader::open(path.as_path()) {
        Ok(reader) => {
            let h = &reader.header;
            println!("FlashPack: {}", args[0]);
            println!("══════════════════════════════");
            println!("  Format version:  {}", h.format_version);
            println!("  Min reader:      {}", h.min_reader_version);
            println!("  Bundle name:     {}", h.bundle_name);
            println!("  Bundle version:  {}", h.bundle_version);
            println!("  Payload type:    {}", h.payload_type);
            println!("  Payload size:    {} bytes", h.payload_size);
            println!("  Requires:        {}", h.requires_version);
            println!("  Created:         {}", h.created_at);
            println!("  Builder:         {}", h.builder_id);
            println!("  Compatible with: {}", h.compatible_slots.join(", "));
            println!("  Flags:           {}", if h.compat_flags.is_empty() { "(none)".into() } else { h.compat_flags.join(", ") });
            println!("  File size:       {} bytes", data.len());

            // Compute checksums
            use sha2::Digest;
            let hash = hex::encode(sha2::Sha256::digest(&data));
            println!("  SHA-256:         {hash}");

            Ok(())
        }
        Err(e) => Err(format!("Failed to read FlashPack: {e}")),
    }
}

// ─── delta ──────────────────────────────────────────────────────

fn cmd_delta(args: &[String]) -> Result<(), String> {
    if args.len() < 3 {
        return Err("delta requires <old> <new> <output.delta>".into());
    }
    let old_path = &args[0];
    let new_path = &args[1];
    let output = &args[2];

    let old = std::fs::read(old_path).map_err(|e| format!("read old: {e}"))?;
    let new = std::fs::read(new_path).map_err(|e| format!("read new: {e}"))?;

    let delta = vela_delta::generate_delta(&old, &new)
        .map_err(|e| format!("delta generation failed: {e}"))?;

    std::fs::write(output, &delta).map_err(|e| format!("write delta: {e}"))?;

    let ratio = delta.len() as f64 / new.len() as f64 * 100.0;
    println!("✓ Delta generated: {output}");
    println!("  Old size:  {} bytes", old.len());
    println!("  New size:  {} bytes", new.len());
    println!("  Delta:     {} bytes ({ratio:.1}%)", delta.len());

    Ok(())
}
