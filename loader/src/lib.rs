use std::io::Read;
use wasmer_wasi::{Pipe, WasiState};
use wasmer::{EngineBuilder, Instance, Module, Store, Cranelift};

#[cfg(feature = "wasi_jit")]
#[no_mangle]
pub fn load_jit(code_ptr: *const u8, code_size: u32, n: u32, result_ptr: *mut u8) -> u32 {
    let mut store = Store::new(Cranelift::default());
    let code = unsafe { std::slice::from_raw_parts(code_ptr, code_size as usize) };
    let module = Module::new(&store, code).unwrap();

    let mut buf = Vec::<u8>::new();
    run(&mut store, &module, n, &mut buf);

    let buf_len = buf.len();
    if buf_len == 0 {
        return 0;
    }

    let dst = unsafe { std::slice::from_raw_parts_mut(result_ptr, buf_len) };
    dst.copy_from_slice(&buf);
    return buf_len as u32;
}

#[cfg(not(feature = "wasi_jit"))]
#[no_mangle]
pub fn load(code_ptr: *const u8, code_size: u32, n: u32, result_ptr: *mut u8) -> u32 {
    let engine = EngineBuilder::headless();
    let mut store = Store::new(engine);
        
    let code = unsafe { std::slice::from_raw_parts(code_ptr, code_size as usize) };
    let module =  unsafe { Module::deserialize(&store, code) }.unwrap();

    let mut buf = Vec::<u8>::new();
    run(&mut store, &module, n, &mut buf);

    let buf_len = buf.len();
    if buf_len == 0 {
        return 0;
    }

    let dst = unsafe { std::slice::from_raw_parts_mut(result_ptr, buf_len) };
    dst.copy_from_slice(&buf);
    return buf_len as u32;
}

fn run(store: &mut Store, module: &Module, n: u32, buf: &mut Vec<u8>) {
    let mut output = Pipe::new();
    let wasi_env = WasiState::new("mandelbrot")
        .args(&[n.to_string()])
        .stdout(Box::new(output.clone()))
        .finalize(store)
        .unwrap();
    let import_object = wasi_env.import_object(store, module).unwrap();
    let i_res = Instance::new(store, module, &import_object);
    if !i_res.is_ok() {
        return;
    }
    let instance = i_res.unwrap();

    let memory = instance.exports.get_memory("memory").unwrap();
    wasi_env.data_mut(store).set_memory(memory.clone());

    let start = instance.exports.get_function("_start").unwrap();
    let s_res = start.call(store, &[]);
    if !s_res.is_ok() {
        return;
    }

    output.read_to_end(buf).unwrap();
}
