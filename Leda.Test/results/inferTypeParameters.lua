local function f(t, u)
  return {a = t, b = u}
end
local t = f("a", 123)
local function g()
  local function d(t, u)
    return t, u
  end
  local a, b = d(123, 456)
end
