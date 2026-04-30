local x, y = true, false
local function f()
  if x then
    return 123
  elseif y then
    return 456
  end
  local d = 123
  return d
end
local function g()
  if x then
    goto A
    local d = 123
    ::A::
    return 123
  else
    return 456
  end
  local x = 123
end
repeat
  if g() == 123 then
    break
  else
    break
  end
  g()
until f() == 123
