local function f(a, b, c)
end
f(123)
f(123, nil, nil, 456)
f()
local function g(h)
  h(123)
  h(123, "a")
end
local function h(a, b, c)
end
