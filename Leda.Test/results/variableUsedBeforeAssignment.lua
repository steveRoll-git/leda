local a, b
local function f()
  local okay = a .. b
  return 2, ""
end
local result = a .. b
if 1 > 2 then
  a = 123
elseif 456 > 789 then
  local bad = a
  return
else
  a = 456
end
local result2 = a .. b
if 123 > 456 then
  local no = b
  return
else
  b = "asdf"
end
local result3 = a .. b
do
  local x, y = 123
  local z = y
end
do
  local x, y = f()
  local z = y
end
