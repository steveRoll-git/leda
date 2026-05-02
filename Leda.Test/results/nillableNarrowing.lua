local function f(x, y)
  local r = x
  if x then
    local r1 = x + 1
  else
    local r1 = x
  end
  local r2 = x + 1
  local r3 = x
  if x then
    local r1 = x + 1
    if 123 then
      local a = y
    end
    return
  end
  local r4 = x
end
local function g(t)
  local r = t.x
  if t.x then
    local r2 = t.x
  else
    local r2 = t.x
  end
  if not not t.x then
    local a = t.x
  end
  if not t.x then
    return
  end
  local r3 = t.x
end
