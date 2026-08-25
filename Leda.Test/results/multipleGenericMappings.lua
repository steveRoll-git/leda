local function a(t)
  return {a = t}
end
local function b(u)
  return a(u)
end
local function c(v)
  return b(v)
end
local result = c(123)
local a1 = result.a
local result2 = c("abc")
local a2 = result2.a
