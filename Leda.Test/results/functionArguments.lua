local function f(p, s)
  local y = s
  local x = p
end
f(123, 456)
local function g()
  return 123, true
end
f(g())
