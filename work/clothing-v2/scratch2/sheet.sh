#!/bin/zsh
cd ${0:A:h:h}/shoes/sheet
T=$1; shift
python3 ../../py/montage.py $T.png ${COLS:-3} $@
