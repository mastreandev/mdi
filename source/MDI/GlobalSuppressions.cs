using System.Diagnostics.CodeAnalysis;

[assembly: SuppressMessage("Design", "CA1027: Mark enums with FlagsAttribute",
    Justification = "HrMode represents mutually exclusive 3-bit wire codes from the Philips M1350 protocol, not combinable flags.",
    Scope = "type",
    Target = "~T:MDI.Philips.M1350.Application.CTG.HrMode"
)]

[assembly: SuppressMessage("Design", "CA1008: Enums should have zero value",
    Justification = "NoTransducer is the protocol term for the zero wire code and is clearer than a generic None value.",
    Scope = "type",
    Target = "~T:MDI.Philips.M1350.Application.CTG.HrMode"
)]
