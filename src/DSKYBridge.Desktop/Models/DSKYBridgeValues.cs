namespace DSKYBridge
{
    public sealed class CMCValues
    {
        public bool IsInCM { get; set; }

        public string ProgramD1 { get; set; } = "";
        public string ProgramD2 { get; set; } = "";

        public string VerbD1 { get; set; } = "";
        public string VerbD2 { get; set; } = "";

        public string NounD1 { get; set; } = "";
        public string NounD2 { get; set; } = "";

        public string Register1D1 { get; set; } = "";
        public string Register1D2 { get; set; } = "";
        public string Register1D3 { get; set; } = "";
        public string Register1D4 { get; set; } = "";
        public string Register1D5 { get; set; } = "";
        public string Register1Sign { get; set; } = "";

        public string Register2D1 { get; set; } = "";
        public string Register2D2 { get; set; } = "";
        public string Register2D3 { get; set; } = "";
        public string Register2D4 { get; set; } = "";
        public string Register2D5 { get; set; } = "";
        public string Register2Sign { get; set; } = "";

        public string Register3D1 { get; set; } = "";
        public string Register3D2 { get; set; } = "";
        public string Register3D3 { get; set; } = "";
        public string Register3D4 { get; set; } = "";
        public string Register3D5 { get; set; } = "";
        public string Register3Sign { get; set; } = "";

        // New fields present in outputAGC.json
        public bool IsFlashing { get; set; }
        public bool HideVerb { get; set; }
        public bool HideNoun { get; set; }
        public float BrightnessNumerics { get; set; }
        public float BrightnessIntegral { get; set; }

        public bool IlluminateCompLight { get; set; }
        public int IlluminateUplinkActy { get; set; }
        public int IlluminateNoAtt { get; set; }
        public int IlluminateStby { get; set; }
        public int IlluminateKeyRel { get; set; }
        public int IlluminateOprErr { get; set; }
        public int IlluminateTemp { get; set; }
        public int IlluminateGimbalLock { get; set; }
        public int IlluminateProg { get; set; }
        public int IlluminateRestart { get; set; }
        public int IlluminateTracker { get; set; }
    }

    public sealed class LGCValues
    {
        public bool IsInCM { get; set; }

        public string ProgramD1 { get; set; } = "";
        public string ProgramD2 { get; set; } = "";

        public string VerbD1 { get; set; } = "";
        public string VerbD2 { get; set; } = "";

        public string NounD1 { get; set; } = "";
        public string NounD2 { get; set; } = "";

        public string Register1D1 { get; set; } = "";
        public string Register1D2 { get; set; } = "";
        public string Register1D3 { get; set; } = "";
        public string Register1D4 { get; set; } = "";
        public string Register1D5 { get; set; } = "";
        public string Register1Sign { get; set; } = "";

        public string Register2D1 { get; set; } = "";
        public string Register2D2 { get; set; } = "";
        public string Register2D3 { get; set; } = "";
        public string Register2D4 { get; set; } = "";
        public string Register2D5 { get; set; } = "";
        public string Register2Sign { get; set; } = "";

        public string Register3D1 { get; set; } = "";
        public string Register3D2 { get; set; } = "";
        public string Register3D3 { get; set; } = "";
        public string Register3D4 { get; set; } = "";
        public string Register3D5 { get; set; } = "";
        public string Register3Sign { get; set; } = "";

        // New fields present in outputAGC.json
        public bool IsFlashing { get; set; }
        public bool HideVerb { get; set; }
        public bool HideNoun { get; set; }
        public float BrightnessNumerics { get; set; }
        public float BrightnessIntegral { get; set; }

        public bool IlluminateCompLight { get; set; }
        public int IlluminateUplinkActy { get; set; }
        public int IlluminateNoAtt { get; set; }
        public int IlluminateStby { get; set; }
        public int IlluminateKeyRel { get; set; }
        public int IlluminateOprErr { get; set; }
        public int IlluminateTemp { get; set; }
        public int IlluminateGimbalLock { get; set; }
        public int IlluminateProg { get; set; }
        public int IlluminateRestart { get; set; }
        public int IlluminateTracker { get; set; }
        public int IlluminateAlt { get; set; }
        public int IlluminateVel { get; set; }
    }
}
