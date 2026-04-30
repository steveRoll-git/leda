local function f()
  return 123
end
local y = f()
y = 456
local z = y
z = f()
z = nil
local s = z
local s2 = z
