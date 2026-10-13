"""Design numbers per hero (metres, mesh space z = height above the floor). Measured off the clothing-v2 target sheets against our body."""


def design(hero_id):
    male = hero_id.endswith('_Male')
    golf = hero_id.startswith('Golf')
    D = {}
    if male and not golf:
        D['order'] = ['socks', 'shoes', 'bottom', 'top']
        D['socks'] = dict(builder='shoes.build_socks', top=.205, bottom=.035, clear=.001, cuff=.022, thickness=.0015)
        D['shoes'] = dict(builder='shoes.build_shoes', last='sneaker', top_y=[0, .30, .45, .62, .80, .92], top_z=[.08, .105, .092, .074, .056, .040], tip_h=.026, insole_z=.004, sole_wrap=.010, sole_bottom=-.031, outsole=.0045, flare=.0045, toe_spring=.011, heel_bevel=.004,
                          toe_room=.012, heel_room=.012, upper_clear=.0065, upper_thick=.0028, collar_back=.078, collar_side=.062, collar_front=.10, ankle_y=.29, toe_box_lift=.007, collar_prof_y=[0, .10, .20, .27, .315, .33], collar_prof_z=[.080, .076, .064, .070, .098, .30],
                          toecap=.80, counter=.20, accent=True, accent_y=.47, accent_slope=.9, accent_w=.026, counter_trim=True, eyestay_w=.012, tongue_w=.062,
                          throat_y0=.66, throat_y1=.30, throat_w0=.024, throat_w1=.040, tongue_rise=.020, lace_rows=6, eyestay_trim=False)
        D['top'] = dict(builder='tops.build_polo', kind='polo', z_hem=.905, z_chest=1.20, z_chest_top=1.34, top_blend=.12, z_neck=1.40, sleeve=.125, sleeve_root=.05,
                        clear=.005, ease_torso=.007, ease_sleeve=.009, hang_slope=.16, sleeve_slope=.01, tipping=True, buttons=2, over=[('Kit_Bottom', .004, .80, 1.02)], g_chest=1.12, g_waist=1.02, g_hem=1.03, z_waist_pattern=1.02, g_sleeve=.39, g_armhole_ease=.03,
                        z_neck_front=1.372, z_neck_back=1.415, neck_r=.095,
                        vent=.035, vent_w=.0035, cuff=.025, collar_n=64, collar_gap=.017, stand_front=.022, stand_back=.030, fall_front=.040, fall_back=.044,
                        point_extra=.024, thickness=.0025, placket_top=1.362, placket_len=.115, placket_w=.030, placket_lift=.0016, button_gap=.042)
        D['bottom'] = dict(builder='bottoms.build_pants', role='Kit_Shorts', flat_front=True, z_waist_front=.968, z_waist_back=.985, s_hem=.315, leg_slope=.06,
                           ease_seat=.008, ease_leg=.016, clear=.005, slit=.035, slit_w=.003, band=.038, band_rib=True, drawcord=True, hem=.025, pockets=True, back_pockets=1, z_crotch=.745, g_seat=1.10, z_seat=.84, g_thigh=.70, g_knee=.66, g_hem=.64)
    if not male and not golf:
        D['order'] = ['socks', 'shoes', 'bottom', 'top']
        D['socks'] = dict(builder='shoes.build_socks', top=.098, bottom=.032, clear=.001, cuff=.016, thickness=.0015, pompom=.011)
        D['shoes'] = dict(builder='shoes.build_shoes', last='sneaker', top_y=[0, .30, .45, .62, .80, .92], top_z=[.072, .095, .083, .067, .051, .036], tip_h=.024, insole_z=.004, sole_wrap=.009, sole_bottom=-.025, outsole=.004, flare=.004, toe_spring=.009, heel_bevel=.003,
                          toe_room=.010, heel_room=.011, upper_clear=.006, upper_thick=.0025, collar_back=.072, collar_side=.056, collar_front=.09, ankle_y=.29,
                          toe_box_lift=.006, collar_prof_y=[0, .10, .20, .27, .315, .33], collar_prof_z=[.072, .068, .057, .062, .088, .30],
                          toecap=.80, counter=.20, accent=True, accent_y=.47, accent_slope=.9, accent_w=.022, counter_trim=True, eyestay_w=.010, tongue_w=.046,
                          throat_y0=.62, throat_y1=.30, throat_w0=.018, throat_w1=.030, tongue_rise=.018, lace_rows=5, eyestay_trim=False)
        D['top'] = dict(builder='tops.build_tank', z_hem=.965, z_chest=1.17, z_chest_top=1.23, z_neck=1.40, sleeve=.02, sleeve_root=.0,
                        clear=.004, ease_torso=.006, ease_sleeve=.006, hang_slope=.14, sleeve_slope=.0,
                        front_z=[1.15, 1.20, 1.28, 1.36, 1.45], front_x=[.24, .165, .125, .10, .095],
                        back_z=[1.10, 1.16, 1.22, 1.30, 1.38, 1.45], back_x=[.24, .14, .075, .062, .09, .095], z_arm_floor=1.15,
                        v_bottom=1.24, v_slope=2.2, z_back_neck=1.33, back_strap_x=.058, strap_top=1.40, binding=.009, hem_band=.024, thickness=.002, g_chest=.86, g_waist=.76, g_hem=.84, z_waist_pattern=1.05,
                        over=[('Kit_Bottom', .003, .90, 1.03)])
        D['bottom'] = dict(builder='bottoms.build_skort',
            under=dict(role='Kit_ShortsBand', z_waist_front=.975, z_waist_back=.985, s_hem=.272, leg_slope=.0, ease_seat=.002, ease_leg=.002, clear=.0025,
                       band=.02, hem=.012, fabric=7),
            skirt=dict(z_top=.955, z_hip=.86, z_hem=.665, flare=.36, g_hem=1.48, ease=.006, pleats=22, pleat_depth=.012, stitch_down=.06, clear=.006, thickness=.0012, cols_per_pleat=8, rows=22),
            band=dict(z_bot=.948, z_top=1.005, ease=.002, thickness=.003, fabric=9))
    if golf:
        tuck = .985 if male else .975
        D['order'] = ['shoes', 'top', 'bottom', 'belt', 'gloves']
        D['shoes'] = dict(builder='shoes.build_shoes', last='sneaker', top_y=[0, .30, .45, .62, .80, .92], top_z=([.08, .102, .090, .072, .055, .039] if male else [.072, .093, .082, .066, .050, .035]), tip_h=.025 if male else .023, insole_z=.003, sole_wrap=.008, sole_bottom=-.033 if male else -.028, outsole=.0035, flare=.0035, toe_spring=.009, heel_bevel=.004,
                          toe_room=.012 if male else .010, heel_room=.012 if male else .011, upper_clear=.0055, upper_thick=.0026, collar_back=.078 if male else .072, collar_side=.062 if male else .056,
                          collar_front=.10, ankle_y=.29, toe_box_lift=.006, collar_prof_y=[0, .10, .20, .27, .315, .33],
                          collar_prof_z=([.080, .076, .064, .070, .098, .30] if male else [.072, .068, .057, .062, .088, .30]),
                          toecap=.80, counter=.20, accent=False, counter_trim=False, eyestay_w=.012, tongue_w=.058 if male else .045,
                          throat_y0=.62, throat_y1=.30, throat_w0=.022 if male else .018, throat_w1=.036 if male else .030, tongue_rise=.016, lace_rows=6 if male else 5,
                          eyestay_trim=False, nubs=True, nub_spacing=.016, nub_r=.0042, nub_h=.003)
        if male:
            D['top'] = dict(builder='tops.build_polo', kind='polo', z_hem=tuck - .07, z_chest=1.20, z_chest_top=1.34, top_blend=.12, z_neck=1.40, sleeve=.13, sleeve_root=.05,
                            clear=.006, ease_torso=.011, ease_sleeve=.016, hang_slope=.10, sleeve_slope=.02, tipping=False, rib_cuff=False, buttons=3,
                            z_neck_front=1.372, z_neck_back=1.415, neck_r=.095, vent=0, cuff=.022, collar_n=64, collar_gap=.017, stand_front=.022, stand_back=.030,
                            fall_front=.040, fall_back=.045, point_extra=.026, thickness=.0025, placket_top=1.362, placket_len=.145, placket_w=.030,
                            placket_lift=.0016, button_gap=.040, tuck_z=tuck, tuck_ease=.004, blouse=.006, raglan=True, raglan_neck_x=.07, g_chest=1.10, g_waist=1.00, g_hem=.99, z_waist_pattern=1.05, g_sleeve=.37, g_armhole_ease=.0)
            D['bottom'] = dict(builder='bottoms.build_pants', role='Kit_Shorts', flat_front=True, z_waist_front=tuck + .005, z_waist_back=tuck + .02, s_hem=.86, leg_slope=.12,
                               ease_seat=.009, ease_leg=.008, clear=.006, slit=0, band=.040, band_rib=False, drawcord=False, hem=.035, extend_to=-.02,
                               hem_z_front=.092, hem_z_back=.048, g_seat=1.12, z_seat=.84, g_thigh=.70, g_knee=.48, g_hem=.40, loops=True, pockets=True, back_pockets=2, fly=True, crease=True, loop_angles=[-150, -100, -45, 45, 100, 150, 180], loop_lift=.0062,
                               over=[('Kit_Top', .0025, tuck - .09, tuck + .05), ('Kit_Shoe_L', .003, -.1, .2), ('Kit_Shoe_R', .003, -.1, .2)])
            D['belt'] = dict(builder='bottoms.build_belt', over='Kit_Bottom', z_mid=tuck - .015, width=.034, thickness=.0035, tip_len=.075,
                             buckle_w=.046, buckle_h=.040, buckle_bar=.006, buckle_depth=.004, buckle_x=.0)
        else:
            D['top'] = dict(builder='tops.build_polo', kind='polo', z_hem=tuck - .07, z_chest=1.17, z_chest_top=1.31, top_blend=.11, z_neck=1.38, sleeve=.12, sleeve_root=.04,
                            clear=.005, ease_torso=.008, ease_sleeve=.010, hang_slope=.12, sleeve_slope=.0, tipping=False, rib_cuff=False, buttons=3,
                            z_neck_front=1.352, z_neck_back=1.395, neck_r=.085, vent=0, cuff=.02, collar_n=60, collar_gap=.014, stand_front=.018, stand_back=.025,
                            fall_front=.034, fall_back=.038, point_extra=.022, thickness=.0022, placket_top=1.342, placket_len=.12, placket_w=.026,
                            placket_lift=.0015, button_gap=.036, tuck_z=tuck, tuck_ease=.003, blouse=.005, g_chest=.88, g_waist=.76, g_hem=.80, z_waist_pattern=1.06, g_sleeve=.31)
            D['bottom'] = dict(builder='bottoms.build_skort',
                under=dict(role='Kit_Shorts', z_waist_front=tuck + .005, z_waist_back=tuck + .015, s_hem=.29, leg_slope=.0, ease_seat=.003, ease_leg=.004, clear=.003,
                           band=.02, hem=.012, fabric=6, over=[('Kit_Top', .0025, tuck - .09, tuck + .05)]),
                skirt=dict(z_top=tuck - .025, z_hip=.86, z_hem=.60, flare=.26, g_hem=1.30, ease=.007, pleats=22, pleat_depth=.012, stitch_down=.08, clear=.006, thickness=.0012, cols_per_pleat=8, rows=22,
                           hem_side_rise=.0),
                band=dict(z_bot=tuck - .03, z_top=tuck + .012, ease=.002, thickness=.003, fabric=6))
            D['belt'] = dict(builder='bottoms.build_belt', over='Kit_Bottom', z_mid=tuck - .01, width=.028, thickness=.003, tip_len=.06,
                             buckle_w=.036, buckle_h=.032, buckle_bar=.005, buckle_depth=.0035, buckle_x=.0)
        D['gloves'] = dict(builder='gloves.build_gloves', clear=.0009, cuff_len=.028, thickness=.0012)
    return D
