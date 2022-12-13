use std::io::Write;
use std::env;

fn main() {
    let current_dir = env::current_dir().unwrap();
    let args: Vec<String> = env::args().collect();
    let path = format!("{}/{}", current_dir.to_str().unwrap(), &args[1]);
    let exec_file = std::fs::read(&path).unwrap();
    let n: usize = args[2].parse().unwrap();
    let mut out = vec![0; ((n * n / 8) + 100) as usize];
    let out_size = loader::load(exec_file.as_ptr(), exec_file.len() as u32, n as u32, out.as_mut_ptr());
    out.resize(out_size as usize, 0);
    std::io::stdout().write_all(&out).unwrap();
}
