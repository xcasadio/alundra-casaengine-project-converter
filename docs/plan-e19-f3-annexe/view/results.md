## 0. The compositor against the binary-derived image of the f2b1 annex
- compose_textbox(rest) vs docs/plan-e19-f2b1-annexe/pixels/rest.png (x1): 0 texels 

## 1. Machine (real AlundraChoiceBox) and proposed view model, pass by pass, against the binary tables
- 428 passes over 10 scenarios (V1 to V7 and V10): 0 with a difference

## 2. Pixels of the proposed screen on this machine's GPU against the independent compositor
- choice alone, every pass of V1-V7, V10: 616 renders, 616 exact (0 texels), 0 texels differ in total (largest 0)
- x3 slide / rest: 9 renders, 9 exact (0 texels), 0 texels differ in total (largest 0)
- view offset (97, 41) in a larger target (window not 4:3): 26 renders, 26 exact (0 texels), 0 texels differ in total (largest 0)
- choice over the text box (text box first, choice above): 40 renders, 40 exact (0 texels), 0 texels differ in total (largest 0)
- control: windows added in the wrong order (must DIFFER): 4 renders, 0 exact (0 texels), 1366 texels differ in total (largest 345)
- failures (non-control): 0
- sensitivity (a render against the reference of a neighbouring pass must differ): V1 t5 vs reference V1 t4: 1078; V1 t5 vs reference V1 t6: 1348; V1 t25 vs reference V1 t24: 2630; V1 t19 vs reference V2 t22: 192; V1 t19 vs reference V1 t21: 3492; V2 t22 vs reference V2 t24: 3516

## 2b. Which XAML attributes carry the pixels (each removed alone from a copy of the proposed XAML; V1 N+19 and V2 N+22, x1, texels differing from the reference)
- frame_by_name: V1 t19: 0 texels; V2 t22: 0 texels
- no_label_foreground: V1 t19: 0 texels; V2 t22: 0 texels
- no_label_inline_off: V1 t19: 0 texels; V2 t22: 0 texels
- no_label_linepadding: V1 t19: 0 texels; V2 t22: 0 texels
- no_label_padding: V1 t19: 248 texels (first at (194, 154) got (152, 160, 128) want (41, 49, 16)); V2 t22: 248 texels (first at (194, 154) got (152, 160, 128) want (41, 49, 16))
- no_label_vca: V1 t19: 0 texels; V2 t22: 0 texels
- no_label_wraptext: V1 t19: 0 texels; V2 t22: 0 texels
- no_window_border0: V1 t19: 5375 texels (first at (0, 0) got (0, 0, 0) want (100, 149, 237)); V2 t22: 5384 texels (first at (0, 0) got (0, 0, 0) want (100, 149, 237))
- no_window_padding0: V1 t19: 3910 texels (first at (197, 138) got (100, 149, 237) want (192, 192, 192)); V2 t22: 3931 texels (first at (245, 137) got (100, 149, 237) want (192, 192, 192))

## 3. One-frame view lag (O-E19-65) measured on the choice: Update, then Apply, then Draw (no further update)
- DYN_V1: A: layout and sprite of this pass: 7, none: 1, B: layout of the previous pass, sprite of this one: 29
    t1 -> A: layout and sprite of this pass
    t2 -> none (A=3738; C=3477)
    t17 -> A: layout and sprite of this pass
    t18 -> A: layout and sprite of this pass
    t19 -> A: layout and sprite of this pass
    t20 -> A: layout and sprite of this pass
    t36 -> A: layout and sprite of this pass
    t37 -> A: layout and sprite of this pass
- DYN_V2: A: layout and sprite of this pass: 9, none: 1, B: layout of the previous pass, sprite of this one: 30
    t1 -> A: layout and sprite of this pass
    t2 -> none (A=3738; C=3477)
    t17 -> A: layout and sprite of this pass
    t18 -> A: layout and sprite of this pass
    t20 -> A: layout and sprite of this pass
    t21 -> A: layout and sprite of this pass
    t22 -> A: layout and sprite of this pass
    t23 -> A: layout and sprite of this pass
    t39 -> A: layout and sprite of this pass
    t40 -> A: layout and sprite of this pass

## 3b. The engine frame order with the real presenters (UI update, pass + presenters, draw): first push, second push, push after a cancel
- fo_a: choice opened at tick 20; view events: 22 push; 57 remove
    first open: nothing drawn: 9, layout of the previous pass + sprite of this one: 33
- fo_b: choice opened at tick 20, second one opened at tick 90; view events: 22 push; 57 remove; 92 push; 127 remove
    first open: nothing drawn: 9, layout of the previous pass + sprite of this one: 33
    second open: nothing drawn: 9, layout of the previous pass + sprite of this one: 33
- fo_c: choice opened at tick 20, cancelled at tick 30, second one opened at tick 90; view events: 22 push; 31 remove; 92 push; 127 remove
    first open: nothing drawn: 5, layout of the previous pass + sprite of this one: 8
      rel 11: nothing drawn where the layout of the previous pass is on screen
    second open: nothing drawn: 8, other: 1, layout of the previous pass + sprite of this one: 33
      rel 2 (tick 92): drawn bbox x 234..319 y 138..175, 2388 texels

## 4. Integrated run: the real director, the real text box presenter, the proposed choice presenter, a stand-in view doing what ScreenStack does
- book_oui: choice opened at tick 61, result 1 at tick 98 (N+37)
  view events: tick 1 push textbox (1 windows); tick 63 push choice (2 windows); tick 98 remove choice (1 windows); tick 134 remove textbox (0 windows)
  the choice window is on the desktop exactly on the drawn passes N+2 .. : 0 tick(s) differ over N+1 .. N+37
  pixels at N+[1, 2, 3, 5, 19, 25, 36, 37, 38]: 9 exact, 0 differ
- book_non: choice opened at tick 61, result 0 at tick 99 (N+38)
  view events: tick 1 push textbox (1 windows); tick 63 push choice (2 windows); tick 99 remove choice (1 windows); tick 134 remove textbox (0 windows)
  the choice window is on the desktop exactly on the drawn passes N+2 .. : 0 tick(s) differ over N+1 .. N+38
  pixels at N+[2, 19, 22, 30, 37, 38, 39]: 7 exact, 0 differ

## 5. Reference images written under view/pixels/
- 19 images (x1, x2) in view/pixels/
