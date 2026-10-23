local held = false
local origin = 0
local timer
local function finish()
 if not held then return end
 held = false
 if timer then timer:kill(); timer = nil end
 mp.commandv('script-message', 'cm-shuttle-end')
end
mp.add_forced_key_binding('MBTN_RIGHT','inferno-shuttle',function(event)
 if event.event == 'down' then
  origin = select(1,mp.get_mouse_pos())
  held = true
  mp.commandv('script-message','cm-shuttle-begin')
  mp.commandv('script-message','cm-shuttle-distance','0')
  if timer then timer:kill() end
  local last = 0
  timer = mp.add_periodic_timer(0.04,function()
   if not held then return end
   local x = select(1,mp.get_mouse_pos())
   local distance = x-origin
   if distance ~= last then
    last = distance
    mp.commandv('script-message','cm-shuttle-distance',tostring(distance))
   end
  end)
 elseif event.event == 'up' or event.event == 'canceled' then finish() end
end,{complex=true})
mp.observe_property('focused','bool',function(_,focus) if focus == false then finish() end end)

mp.register_script_message('inferno-cancel-shuttle',finish)

mp.register_script_message('inferno-check-mouse',function() mp.commandv('script-message','inferno-mouse-ready') end)
