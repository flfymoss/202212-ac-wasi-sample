package main

import (
	"fmt"
)

func fib(n int32) int32 {
	switch n {
	case 0:
		return 0
	case 1:
		return 1
	default:
		return fib(n-2) + fib(n-1)
	}
}

const N int32 = 42

func main() {
	fmt.Printf("fib(%d) is %d\n", N, fib(N))
}

