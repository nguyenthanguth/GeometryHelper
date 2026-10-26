namespace GeometryHelper.Dxf
{
    /// <summary>
    /// The time zone of the drawing's place, as AutoCAD's <c>TimeZone</c> names and numbers it (<c>$TIMEZONE</c>, group 70):
    /// the hours from UTC times 1 000, the minutes times 10, and a last digit telling apart the zones of one offset, so that
    /// <see cref="Kathmandu"/>, five hours and three quarters ahead, is 5 450.
    /// </summary>
    public enum DxfTimeZone
    {
        /// <summary>International Date Line, UTC-12:00.</summary>
        InternationalDateLine = -12000,

        /// <summary>Midway Island, UTC-11:00.</summary>
        MidwayIsland = -11000,

        /// <summary>Hawaii, UTC-10:00.</summary>
        Hawaii = -10000,

        /// <summary>Alaska, UTC-09:00.</summary>
        Alaska = -9000,

        /// <summary>Pacific, UTC-08:00.</summary>
        Pacific = -8000,

        /// <summary>Mountain, UTC-07:00.</summary>
        Mountain = -7000,

        /// <summary>Arizona, UTC-07:00.</summary>
        Arizona = -7001,

        /// <summary>Mazatlan, UTC-07:00.</summary>
        Mazatlan = -7002,

        /// <summary>Central, UTC-06:00.</summary>
        Central = -6000,

        /// <summary>Central America, UTC-06:00.</summary>
        CentralAmerica = -6001,

        /// <summary>Mexico City, UTC-06:00.</summary>
        MexicoCity = -6002,

        /// <summary>Saskatchewan, UTC-06:00.</summary>
        Saskatchewan = -6003,

        /// <summary>Eastern, UTC-05:00.</summary>
        Eastern = -5000,

        /// <summary>Indiana, UTC-05:00.</summary>
        Indiana = -5001,

        /// <summary>Bogota, UTC-05:00.</summary>
        Bogota = -5002,

        /// <summary>Atlantic Canada, UTC-04:00.</summary>
        AtlanticCanada = -4000,

        /// <summary>Caracas, UTC-04:00.</summary>
        Caracas = -4001,

        /// <summary>Santiago, UTC-04:00.</summary>
        Santiago = -4002,

        /// <summary>Newfoundland, UTC-03:30.</summary>
        Newfoundland = -3300,

        /// <summary>Brasilia, UTC-03:00.</summary>
        Brasilia = -3000,

        /// <summary>Buenos Aires, UTC-03:00.</summary>
        BuenosAires = -3001,

        /// <summary>Greenland, UTC-03:00.</summary>
        Greenland = -3002,

        /// <summary>Mid Atlantic, UTC-02:00.</summary>
        MidAtlantic = -2000,

        /// <summary>Azores, UTC-01:00.</summary>
        Azores = -1000,

        /// <summary>Cape Verde, UTC-01:00.</summary>
        CapeVerde = -1001,

        /// <summary>UTC, UTC+00:00.</summary>
        UTC = 0,

        /// <summary>GMT, UTC+00:00.</summary>
        GMT = 1,

        /// <summary>Monrovia, UTC+00:00.</summary>
        Monrovia = 2,

        /// <summary>Berlin, UTC+01:00.</summary>
        Berlin = 1000,

        /// <summary>Paris, UTC+01:00.</summary>
        Paris = 1001,

        /// <summary>Prague, UTC+01:00.</summary>
        Prague = 1002,

        /// <summary>Sarajevo, UTC+01:00.</summary>
        Sarajevo = 1003,

        /// <summary>West Central Africa, UTC+01:00.</summary>
        WestCentralAfrica = 1004,

        /// <summary>Athens, UTC+02:00.</summary>
        Athens = 2000,

        /// <summary>Eastern Europe, UTC+02:00.</summary>
        EasternEurope = 2001,

        /// <summary>Cairo, UTC+02:00.</summary>
        Cairo = 2002,

        /// <summary>Harare, UTC+02:00.</summary>
        Harare = 2003,

        /// <summary>Helsinki, UTC+02:00.</summary>
        Helsinki = 2004,

        /// <summary>Jerusalem, UTC+02:00.</summary>
        Jerusalem = 2005,

        /// <summary>Moscow, UTC+03:00.</summary>
        Moscow = 3000,

        /// <summary>Riyadh, UTC+03:00.</summary>
        Riyadh = 3001,

        /// <summary>Baghdad, UTC+03:00.</summary>
        Baghdad = 3002,

        /// <summary>East Africa, UTC+03:00.</summary>
        EastAfrica = 3003,

        /// <summary>Tehran, UTC+03:30.</summary>
        Tehran = 3300,

        /// <summary>Abu Dhabi, UTC+04:00.</summary>
        AbuDhabi = 4000,

        /// <summary>Caucasus, UTC+04:00.</summary>
        Caucasus = 4001,

        /// <summary>Kabul, UTC+04:30.</summary>
        Kabul = 4300,

        /// <summary>Ekaterinburg, UTC+05:00.</summary>
        Ekaterinburg = 5000,

        /// <summary>Islamabad, UTC+05:00.</summary>
        Islamabad = 5001,

        /// <summary>Kolkata, UTC+05:30.</summary>
        Kolkata = 5300,

        /// <summary>Kathmandu, UTC+05:45.</summary>
        Kathmandu = 5450,

        /// <summary>Almaty, UTC+06:00.</summary>
        Almaty = 6000,

        /// <summary>Dhaka, UTC+06:00.</summary>
        Dhaka = 6001,

        /// <summary>Sri Lanka, UTC+06:00.</summary>
        SriLanka = 6002,

        /// <summary>Rangoon, UTC+06:30.</summary>
        Rangoon = 6300,

        /// <summary>Bangkok, UTC+07:00.</summary>
        Bangkok = 7000,

        /// <summary>Krasnoyarsk, UTC+07:00.</summary>
        Krasnoyarsk = 7001,

        /// <summary>Beijing, UTC+08:00.</summary>
        Beijing = 8000,

        /// <summary>Singapore, UTC+08:00.</summary>
        Singapore = 8001,

        /// <summary>Taipei, UTC+08:00.</summary>
        Taipei = 8002,

        /// <summary>Irkutsk, UTC+08:00.</summary>
        Irkutsk = 8003,

        /// <summary>Perth, UTC+08:00.</summary>
        Perth = 8004,

        /// <summary>Tokyo, UTC+09:00.</summary>
        Tokyo = 9000,

        /// <summary>Seoul, UTC+09:00.</summary>
        Seoul = 9001,

        /// <summary>Yakutsk, UTC+09:00.</summary>
        Yakutsk = 9002,

        /// <summary>Adelaide, UTC+09:30.</summary>
        Adelaide = 9300,

        /// <summary>Darwin, UTC+09:30.</summary>
        Darwin = 9301,

        /// <summary>Sydney, UTC+10:00.</summary>
        Sydney = 10000,

        /// <summary>Guam, UTC+10:00.</summary>
        Guam = 10001,

        /// <summary>Brisbane, UTC+10:00.</summary>
        Brisbane = 10002,

        /// <summary>Hobart, UTC+10:00.</summary>
        Hobart = 10003,

        /// <summary>Vladivostock, UTC+10:00.</summary>
        Vladivostock = 10004,

        /// <summary>Magadan, UTC+11:00.</summary>
        Magadan = 11000,

        /// <summary>Wellington, UTC+12:00.</summary>
        Wellington = 12000,

        /// <summary>Fiji, UTC+12:00.</summary>
        Fiji = 12001,

        /// <summary>Tonga, UTC+13:00.</summary>
        Tonga = 13000,
    }
}
