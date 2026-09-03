local function f(arr)
  local x = arr[1]
  arr[2] = 45.67
end
local a = {1, 2}
f(a)
f({1, "abc", 3})
local b = {"a", "b"}
f(b)
b[#b] = nil
local c = {function(a, b)
  local c = a + b
end}
