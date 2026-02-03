<wpf-math-latex-guide>
WPF-Math is a LIMITED LaTeX subset. Many standard TeX commands are NOT supported.

<supported-spacing>
\,    thin space (3/18 em) - USE for spacing before units
\:    medium space (4/18 em)
\;    thick space (5/18 em) - USE for separating multiple values
</supported-spacing>

<not-supported>
\quad      FAILS - "Unknown symbol or command"
\qquad     FAILS
\          FAILS (backslash-space)
~          FAILS (tilde)
\hspace    FAILS
\text{}    FAILS
\mathrm{}  FAILS
\boxed{}   FAILS
\begin{}   FAILS (no environments)
</not-supported>

<correct-patterns>
Unit after value:     \rho = 971.82 \, kg/m^{3}
Multiple values:      DN = 40 \; D_i = 41.9 \, mm
Degree symbol:        T = 35 \, {}^{\circ}C
Scientific notation:  \mu = 3.55 \cdot 10^{-4} \, Pa \cdot s
Fractions:            \frac{numerator}{denominator}
Subscripts:           T_{retur}
Superscripts:         m^{3}
</correct-patterns>

<c-sharp-escaping>
In C# interpolated strings, braces must be doubled:
- LaTeX {x} becomes C# {{x}}
- Example: $@"m^{{3}}" produces m^{3}
- Example: $@"T_{{retur}}" produces T_{retur}
</c-sharp-escaping>

<validation-test>
Use Tests/LaTeXValidatorTest.cs to validate formulas before deployment.
Call LaTeXValidatorTest.ValidateLaTeX(formula) to check if a formula parses.
</validation-test>
</wpf-math-latex-guide>
