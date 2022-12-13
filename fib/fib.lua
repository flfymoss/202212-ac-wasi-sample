function fib(n)
  if n == 0 then
    return 0
  elseif n == 1 then
    return 1
  else
    return fib(n - 2) + fib(n - 1)
  end
end

N = 42

print(string.format("fib(%d) is %d", N, fib(N)))

