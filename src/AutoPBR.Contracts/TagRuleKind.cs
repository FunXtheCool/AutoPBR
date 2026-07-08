namespace AutoPBR.Contracts;

/// <summary>Material tags are inferred by keywords / MiniLM. Flag tags describe asset location / wrapping (path-derived and optional keywords).</summary>
public enum TagRuleKind
{
    Material,
    Flag
}
