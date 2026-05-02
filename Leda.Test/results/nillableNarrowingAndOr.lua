local function f(x, y)
  local r = x + y
  if x then
    local r2 = x + y
  end
  if x or y then
    local r2 = x + y
  end
  if x and y then
    local r2 = x + y
  end
  if not not x and not not y then
    local r2 = x + y
  end
  if not not x and not y then
    local r2 = x + y
  end
  if not x and not y then
    local x1, y1 = x, y
  end
  if not (not x or not y) then
    local r2 = x + y
  end
  if not x or not y then
    return
  end
  local r3 = x + y
end
local function g(x, y)
  if x or y then
  else
    local x1, y1 = x, y
  end
end
