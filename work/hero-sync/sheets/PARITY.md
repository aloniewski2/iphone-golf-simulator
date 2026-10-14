# Lobby vs game (hero sync)

LOBBY = the app's SceneKit hero (GolfArcade/Unity/CharacterAssets, MatchHeroSurfaces); GAME = Unity drawing the same hero, same pose, same cameras and lights.
Mean sRGB of the hero pixels; ratio = lobby/game luminance; MAD = mean absolute difference per channel over the hero pixels (0-255).

| shot | lobby mean | game mean | lum ratio | MAD |
|---|---|---|---|---|
| tennis male_default_body | (150,112,111) | (155,116,113) | 0.967 | 5.6 |
| tennis male_default_feet | (117,113,126) | (121,117,130) | 0.966 | 6.1 |
| tennis male_default_head | (178,93,80) | (183,96,82) | 0.968 | 5.6 |
| tennis male_default_torso | (142,99,100) | (147,103,102) | 0.970 | 5.2 |
| tennis male_s1_body | (166,130,127) | (172,135,131) | 0.965 | 6.4 |
| tennis male_s1_feet | (132,130,142) | (138,135,147) | 0.965 | 6.7 |
| tennis male_s1_head | (191,106,93) | (196,110,95) | 0.965 | 6.4 |
| tennis male_s1_torso | (160,118,118) | (165,122,121) | 0.968 | 5.9 |
| tennis male_s2_body | (154,118,96) | (160,122,98) | 0.966 | 5.9 |
| tennis male_s2_feet | (108,108,92) | (113,112,94) | 0.962 | 6.3 |
| tennis male_s2_head | (199,111,87) | (202,114,89) | 0.976 | 5.8 |
| tennis male_s2_torso | (156,106,99) | (161,110,101) | 0.970 | 5.7 |
| tennis male_s3_body | (143,135,142) | (149,140,146) | 0.967 | 6.4 |
| tennis male_s3_feet | (175,156,144) | (182,162,149) | 0.964 | 8.3 |
| tennis male_s3_head | (89,105,146) | (95,108,148) | 0.969 | 5.4 |
| tennis male_s3_torso | (134,128,138) | (138,131,141) | 0.971 | 6.1 |
| tennis male_s4_body | (111,99,73) | (115,102,73) | 0.968 | 5.2 |
| tennis male_s4_feet | (95,65,65) | (99,68,65) | 0.959 | 5.7 |
| tennis male_s4_head | (114,134,71) | (117,136,71) | 0.982 | 4.7 |
| tennis male_s4_torso | (98,102,72) | (102,104,72) | 0.975 | 4.8 |
| tennis male_s5_body | (137,117,90) | (142,121,91) | 0.970 | 6.8 |
| tennis male_s5_feet | (121,111,108) | (127,116,111) | 0.957 | 9.3 |
| tennis male_s5_head | (165,131,67) | (167,132,66) | 0.990 | 5.2 |
| tennis male_s5_torso | (151,128,95) | (155,131,95) | 0.980 | 6.4 |
| tennis female_default_body | (173,156,131) | (180,162,134) | 0.966 | 7.2 |
| tennis female_default_feet | (168,150,134) | (175,156,139) | 0.959 | 8.6 |
| tennis female_default_head | (176,160,130) | (180,163,131) | 0.985 | 6.3 |
| tennis female_default_torso | (173,157,130) | (178,162,133) | 0.971 | 7.0 |
| tennis female_s1_body | (187,173,147) | (194,179,151) | 0.964 | 7.9 |
| tennis female_s1_feet | (182,167,151) | (190,174,157) | 0.958 | 9.2 |
| tennis female_s1_head | (190,177,147) | (195,180,148) | 0.981 | 7.1 |
| tennis female_s1_torso | (187,173,145) | (193,179,149) | 0.969 | 7.5 |
| tennis female_s2_body | (144,119,102) | (150,123,104) | 0.965 | 6.4 |
| tennis female_s2_feet | (108,102,87) | (113,106,89) | 0.959 | 6.4 |
| tennis female_s2_head | (186,145,127) | (190,148,128) | 0.982 | 6.7 |
| tennis female_s2_torso | (145,113,108) | (150,117,110) | 0.969 | 6.3 |
| tennis female_s3_body | (151,131,124) | (156,135,127) | 0.967 | 6.8 |
| tennis female_s3_feet | (155,131,118) | (161,137,121) | 0.960 | 7.9 |
| tennis female_s3_head | (148,130,129) | (152,132,129) | 0.985 | 5.7 |
| tennis female_s3_torso | (148,129,124) | (153,133,127) | 0.971 | 6.4 |
| tennis female_s4_body | (104,84,72) | (108,87,71) | 0.968 | 5.6 |
| tennis female_s4_feet | (82,53,53) | (86,56,52) | 0.956 | 5.7 |
| tennis female_s4_head | (132,120,98) | (134,120,97) | 0.992 | 5.2 |
| tennis female_s4_torso | (99,90,77) | (103,93,77) | 0.974 | 5.4 |
| tennis female_s5_body | (117,100,88) | (121,104,87) | 0.968 | 7.4 |
| tennis female_s5_feet | (96,85,84) | (101,89,84) | 0.954 | 8.9 |
| tennis female_s5_head | (128,110,91) | (130,110,89) | 0.999 | 5.3 |
| tennis female_s5_torso | (130,113,98) | (134,116,98) | 0.977 | 6.8 |
| golf male_default_body | (84,114,117) | (91,119,121) | 0.947 | 7.0 |
| golf male_default_feet | (76,80,93) | (84,87,98) | 0.926 | 8.8 |
| golf male_default_head | (75,157,153) | (83,162,157) | 0.962 | 6.7 |
| golf male_default_torso | (75,113,117) | (82,118,121) | 0.950 | 6.5 |
| golf female_default_body | (158,129,122) | (167,136,126) | 0.950 | 7.9 |
| golf female_default_feet | (128,121,120) | (138,129,126) | 0.932 | 10.7 |
| golf female_default_head | (187,129,117) | (193,134,121) | 0.961 | 7.7 |
| golf female_default_torso | (152,121,116) | (160,128,120) | 0.950 | 7.8 |
