local function ipairs(t)
  return function(t, i)
    return i + 1, t[i]
  end, t, 0
end
local arr = {"a", "b", "c"}
for i, item in ipairs(arr) do
  local j = i
  local e = item
end
for nope, bad in 1, 2, 3 do
end
for nope, nad in ipairs do
end
