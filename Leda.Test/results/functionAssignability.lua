local good = function(a, b)
  if a > 10 then
    return 123, "abc"
  else
    return 456, "def"
  end
end
local bad = function(a, b, c)
end
local bad2 = function(a, b, c)
  return 123, {}
end
