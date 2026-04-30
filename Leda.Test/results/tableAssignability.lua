local good = {a = 123, b = "456"}
local bad = {a = 123}
local bad2 = {}
local bad3 = {a = 123, b = "abc", asdf = {}}
local inferred = {a = 123, b = "abc"}
good = inferred
local bad4 = good
local bad5 = good
