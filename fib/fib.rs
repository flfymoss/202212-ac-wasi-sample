fn fib(n: i32) -> i32 {
    return match n {
        0 => 0,
        1 => 1,
        _ => fib(n - 2) + fib(n - 1)
    }
}

const N: i32 = 42;

fn main() {
    println!("fib({}) is {}", N, fib(42));
}
