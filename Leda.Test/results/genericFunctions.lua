local function f()
  return function(t, u)
    return t, u
  end
end
local x = f()
local y, z = x({x = 123}, "asdf")
local u = y.x
local function g()
  local function d(t, u)
    return t
  end
  local a = d(123, 456)
end
