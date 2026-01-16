namespace DSKYBridge.Core.Protocol.CommandModule
{
    /// <summary>
    /// Apollo Guidance Computer (AGC) button IDs.
    /// Numeric values MUST match ReEntry protocol.
    /// </summary>
    public enum AGCButtonId
    {
        NULL        = 0,

        AGCVerb     = 1,
        AGCNoun     = 2,
        AGCPlus     = 3,
        AGCMinus    = 4,

        AGC0        = 5,
        AGC1        = 6,
        AGC2        = 7,
        AGC3        = 8,
        AGC4        = 9,
        AGC5        = 10,
        AGC6        = 11,
        AGC7        = 12,
        AGC8        = 13,
        AGC9        = 14,

        AGCClear    = 15,
        AGCPro      = 16,
        AGCKeyRel   = 17,
        AGCEntr     = 18,
        AGCRset     = 19,

        // --- Future AGC expansion goes here ---
    }
}
