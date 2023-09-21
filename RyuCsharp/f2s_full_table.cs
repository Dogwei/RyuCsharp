﻿

namespace RyuCsharp;

partial class Ryu
{
    // This table is generated by PrintFloatLookupTable.
    const int FLOAT_POW5_INV_BITCOUNT = 59;
    const int FLOAT_POW5_BITCOUNT = 61;

    static readonly uint64_t[] FLOAT_POW5_INV_SPLIT = new uint64_t[31] {
          576460752303423489u, 461168601842738791u, 368934881474191033u, 295147905179352826u,
          472236648286964522u, 377789318629571618u, 302231454903657294u, 483570327845851670u,
          386856262276681336u, 309485009821345069u, 495176015714152110u, 396140812571321688u,
          316912650057057351u, 507060240091291761u, 405648192073033409u, 324518553658426727u,
          519229685853482763u, 415383748682786211u, 332306998946228969u, 531691198313966350u,
          425352958651173080u, 340282366920938464u, 544451787073501542u, 435561429658801234u,
          348449143727040987u, 557518629963265579u, 446014903970612463u, 356811923176489971u,
          570899077082383953u, 456719261665907162u, 365375409332725730u
        };
    static readonly uint64_t[] FLOAT_POW5_SPLIT = new uint64_t[47] {
          1152921504606846976u, 1441151880758558720u, 1801439850948198400u, 2251799813685248000u,
          1407374883553280000u, 1759218604441600000u, 2199023255552000000u, 1374389534720000000u,
          1717986918400000000u, 2147483648000000000u, 1342177280000000000u, 1677721600000000000u,
          2097152000000000000u, 1310720000000000000u, 1638400000000000000u, 2048000000000000000u,
          1280000000000000000u, 1600000000000000000u, 2000000000000000000u, 1250000000000000000u,
          1562500000000000000u, 1953125000000000000u, 1220703125000000000u, 1525878906250000000u,
          1907348632812500000u, 1192092895507812500u, 1490116119384765625u, 1862645149230957031u,
          1164153218269348144u, 1455191522836685180u, 1818989403545856475u, 2273736754432320594u,
          1421085471520200371u, 1776356839400250464u, 2220446049250313080u, 1387778780781445675u,
          1734723475976807094u, 2168404344971008868u, 1355252715606880542u, 1694065894508600678u,
          2117582368135750847u, 1323488980084844279u, 1654361225106055349u, 2067951531382569187u,
          1292469707114105741u, 1615587133892632177u, 2019483917365790221u
        };
}