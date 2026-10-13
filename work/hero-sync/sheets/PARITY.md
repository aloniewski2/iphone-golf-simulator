# Lobby vs game (hero sync)

LOBBY = the app's SceneKit hero (GolfArcade/Unity/CharacterAssets, MatchHeroSurfaces); GAME = Unity drawing the same hero, same pose, same cameras and lights.
Mean sRGB of the hero pixels; ratio = lobby/game luminance; MAD = mean absolute difference per channel over the hero pixels (0-255).

| shot | lobby mean | game mean | lum ratio | MAD |
|---|---|---|---|---|
| tennis male_default_body | (147,112,111) | (152,116,114) | 0.966 | 5.7 |
| tennis male_default_feet | (112,111,127) | (117,116,132) | 0.960 | 6.3 |
| tennis male_default_head | (182,92,75) | (185,95,76) | 0.974 | 4.5 |
| tennis male_default_torso | (139,98,100) | (143,102,102) | 0.969 | 5.1 |
| tennis male_s1_body | (164,130,128) | (170,135,132) | 0.965 | 6.4 |
| tennis male_s1_feet | (126,126,142) | (132,132,147) | 0.959 | 7.0 |
| tennis male_s1_head | (201,112,94) | (205,116,96) | 0.973 | 5.0 |
| tennis male_s1_torso | (158,118,119) | (163,122,122) | 0.968 | 5.7 |
| tennis male_s2_body | (152,116,98) | (158,120,100) | 0.965 | 5.9 |
| tennis male_s2_feet | (104,104,96) | (110,108,99) | 0.955 | 6.5 |
| tennis male_s2_head | (206,111,82) | (209,113,83) | 0.982 | 4.7 |
| tennis male_s2_torso | (152,104,98) | (157,107,100) | 0.969 | 5.5 |
| tennis male_s3_body | (146,137,142) | (152,142,146) | 0.967 | 6.7 |
| tennis male_s3_feet | (179,160,148) | (186,167,153) | 0.961 | 8.6 |
| tennis male_s3_head | (91,101,138) | (96,104,139) | 0.973 | 4.4 |
| tennis male_s3_torso | (138,130,138) | (143,134,141) | 0.970 | 6.2 |
| tennis male_s4_body | (108,98,73) | (112,101,74) | 0.966 | 5.2 |
| tennis male_s4_feet | (92,69,70) | (97,72,71) | 0.954 | 5.6 |
| tennis male_s4_head | (108,125,58) | (111,126,58) | 0.982 | 3.9 |
| tennis male_s4_torso | (94,97,69) | (98,99,69) | 0.971 | 4.7 |
| tennis male_s5_body | (140,120,94) | (145,124,95) | 0.971 | 6.9 |
| tennis male_s5_feet | (135,125,122) | (141,131,125) | 0.960 | 9.2 |
| tennis male_s5_head | (154,117,51) | (156,119,51) | 0.990 | 4.4 |
| tennis male_s5_torso | (151,128,96) | (154,131,97) | 0.978 | 6.5 |
| tennis female_default_body | (185,171,136) | (189,174,137) | 0.981 | 7.1 |
| tennis female_default_feet | (187,168,151) | (193,173,154) | 0.970 | 9.1 |
| tennis female_default_head | (190,181,120) | (189,179,117) | 1.010 | 5.8 |
| tennis female_default_torso | (185,171,132) | (187,173,131) | 0.991 | 6.9 |
| tennis female_s1_body | (197,185,150) | (201,189,152) | 0.978 | 7.8 |
| tennis female_s1_feet | (201,184,167) | (208,191,172) | 0.966 | 10.1 |
| tennis female_s1_head | (203,196,135) | (202,194,132) | 1.007 | 6.1 |
| tennis female_s1_torso | (198,187,147) | (201,189,148) | 0.987 | 7.5 |
| tennis female_s2_body | (161,125,107) | (163,126,106) | 0.991 | 6.9 |
| tennis female_s2_feet | (127,118,103) | (130,120,104) | 0.983 | 8.1 |
| tennis female_s2_head | (213,140,112) | (212,138,109) | 1.012 | 6.0 |
| tennis female_s2_torso | (162,117,108) | (162,117,106) | 1.004 | 6.8 |
| tennis female_s3_body | (154,139,139) | (157,141,140) | 0.985 | 6.8 |
| tennis female_s3_feet | (176,152,137) | (182,156,138) | 0.974 | 8.2 |
| tennis female_s3_head | (131,124,141) | (129,122,138) | 1.019 | 5.7 |
| tennis female_s3_torso | (149,134,137) | (150,135,136) | 0.997 | 6.5 |
| tennis female_s4_body | (114,101,79) | (115,101,76) | 1.002 | 6.0 |
| tennis female_s4_feet | (100,72,70) | (101,72,67) | 1.006 | 6.4 |
| tennis female_s4_head | (131,133,83) | (130,130,79) | 1.019 | 5.4 |
| tennis female_s4_torso | (105,101,77) | (104,99,73) | 1.017 | 5.8 |
| tennis female_s5_body | (139,118,96) | (141,119,93) | 0.993 | 6.9 |
| tennis female_s5_feet | (121,109,106) | (124,111,104) | 0.987 | 8.4 |
| tennis female_s5_head | (153,122,76) | (151,120,71) | 1.020 | 5.3 |
| tennis female_s5_torso | (147,125,99) | (147,124,95) | 1.004 | 6.7 |
| golf male_default_body | (79,110,115) | (87,115,118) | 0.946 | 6.7 |
| golf male_default_feet | (67,72,87) | (75,78,92) | 0.919 | 8.8 |
| golf male_default_head | (86,171,161) | (94,175,165) | 0.968 | 5.8 |
| golf male_default_torso | (75,114,118) | (82,119,121) | 0.953 | 6.1 |
| golf female_default_body | (153,124,118) | (162,131,123) | 0.946 | 8.2 |
| golf female_default_feet | (114,111,115) | (127,122,122) | 0.910 | 12.3 |
| golf female_default_head | (217,148,127) | (220,151,129) | 0.982 | 5.4 |
| golf female_default_torso | (145,115,112) | (154,122,116) | 0.945 | 8.0 |
