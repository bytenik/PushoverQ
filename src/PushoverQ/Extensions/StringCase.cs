using System.ComponentModel;

namespace PushoverQ
{
    public enum StringCase
    {
        Default,
        [Description("Lower Case")]
        Lower,
        [Description("Upper Case")]
        Upper,
        [Description("Lower Camelcase")]
        LowerCamel,
        [Description("Upper Camelcase")]
        UpperCamel
    }
}
