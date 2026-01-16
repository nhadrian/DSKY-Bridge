namespace DSKYBridge.Core.Protocol.LunarModule
{
    /// <summary>
    /// Lunar Guidance Computer (LGC) button IDs.
    /// Numeric values MUST match ReEntry protocol.
    /// </summary>
    public enum LGCButtonId
    {
        NULL        = 0,

        LGCVerb     = 7,
        LGCNoun     = 8,
        LGCPlus     = 9,
        LGCMinus    = 10,

        LGC0        = 11,
        LGC1        = 12,
        LGC2        = 13,
        LGC3        = 14,
        LGC4        = 15,
        LGC5        = 16,
        LGC6        = 17,
        LGC7        = 18,
        LGC8        = 19,
        LGC9        = 20,

        LGCClr      = 21,
        LGCPro      = 22,
        LGCKeyRel   = 23,
        LGCEntr     = 24,
        LGCRset     = 25,

        // --- Future LGC expansion goes here ---
    }
}
