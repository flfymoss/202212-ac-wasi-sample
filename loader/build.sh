#!/bin/zsh
cargo build --release --lib
cargo build --release --lib --target aarch64-linux-android
cargo build --release --lib --target aarch64-apple-ios
cargo build --release --lib --features wasi_jit --target-dir target_jit
cargo build --release --lib --features wasi_jit --target-dir target_jit --target aarch64-linux-android

