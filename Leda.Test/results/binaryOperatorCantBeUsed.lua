local function f()
  return 123
end
local add = 1 + f()
local sub = f() - 4
local mul = f() * f()
local div = f() / 5.67
local mod = 123 % f()
local pow = f() ^ 2
local addBad = "a" + 2
local g = f() > 1
local ge = 4 >= f()
local l = "a" > "b"
local le = f() <= f()
local leBad = 5 < ""
local conc1 = f() .. "asdf"
local conc2 = "hello " .. "world"
local conc3 = "asdf" .. f()
local conc4 = f() .. f()
local concBad = f() .. {}
local eq = f() == {}
local neq = f() ~= {}
