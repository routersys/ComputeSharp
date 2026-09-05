using Microsoft.CodeAnalysis;

namespace ComputeWeave.SourceGeneration.Diagnostics;

/// <inheritdoc/>
partial class DiagnosticDescriptors
{
    /// <summary>
    /// The diagnostic id for <see cref="MissingComputeShaderDescriptorOnComputeShaderType"/>.
    /// </summary>
    public const string MissingComputeShaderDescriptorOnComputeShaderTypeId = "CMPW0053";

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid shader field.
    /// <para>
    /// Format: <c>"The compute shader of type {0} contains a field "{1}" of an invalid type {2}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidShaderField = new(
        id: "CMPW0001",
        title: "Invalid shader field",
        messageFormat: """The compute shader of type {0} contains a field "{1}" of an invalid type {2}""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type representing a compute shader contains a field of a type that is not supported in HLSL.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid group shared field type.
    /// <para>
    /// Format: <c>"The compute shader of type {0} contains a group shared field "{1}" of an invalid type {2}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGroupSharedFieldType = new(
        id: "CMPW0002",
        title: "Invalid group shared field type",
        messageFormat: """The compute shader of type {0} contains a group shared field "{1}" of an invalid type {2} (it must be an array)""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A group shared field must be of an array type.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid group shared field element type.
    /// <para>
    /// Format: <c>"The compute shader of type {0} contains a group shared field "{1}" of an invalid element type {2}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGroupSharedFieldElementType = new(
        id: "CMPW0003",
        title: "Invalid group shared field element type",
        messageFormat: """The compute shader of type {0} contains a group shared field "{1}" of an invalid type {2} (it must be a primitive or unmanaged type)""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A group shared field element must be of a primitive or unmanaged type.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid group shared field declaration.
    /// <para>
    /// Format: <c>"The field "{0}" is annotated with [GroupShared], but is not a valid target for it (only static fields of array type in compute shader types, with an unmanaged element type can be annotated with [GroupShared])"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGroupSharedFieldDeclaration = new(
        id: "CMPW0004",
        title: "Invalid [GroupShared] field declaration",
        messageFormat: """The field "{0}" is annotated with [GroupShared], but is not a valid target for it (only static fields of array type in compute shader types, with an unmanaged element type can be annotated with [GroupShared])""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GroupShared] attribute is only valid on static fields of array type in compute shader types, with an unmanaged element type.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shader with no resources.
    /// <para>
    /// Format: <c>"The compute shader of type {0} contains no resources to work on"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingShaderResources = new(
        id: "CMPW0005",
        title: "Missing shader resources",
        messageFormat: "The compute shader of type {0} contains no resources to work on",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader must contain at least one resource.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid <see cref="ThreadIds"/> usage.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidThreadIdsUsage = new(
        id: "CMPW0006",
        title: "Invalid ThreadIds usage",
        messageFormat: "The ThreadIds type can only be used within the main body of a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The ThreadIds type can only be used within the main body of a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid <see cref="GroupIds"/> usage.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGroupIdsUsage = new(
        id: "CMPW0007",
        title: "Invalid GroupIds usage",
        messageFormat: "The GroupIds type can only be used within the main body of a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The GroupIds type can only be used within the main body of a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid <see cref="GroupSize"/> usage.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGroupSizeUsage = new(
        id: "CMPW0008",
        title: "Invalid GroupSize usage",
        messageFormat: "The GroupSize type can only be used within the main body of a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The GroupSize type can only be used within the main body of a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid <see cref="GridIds"/> usage.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGridIdsUsage = new(
        id: "CMPW0009",
        title: "Invalid GridIds usage",
        messageFormat: "The GridIds type can only be used within the main body of a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The GridIds type can only be used within the main body of a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid object creation expression.
    /// <para>
    /// Format: <c>"The type {0} cannot be created in a compute shader"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidObjectCreationExpression = new(
        id: "CMPW0010",
        title: "Invalid object creation expression",
        messageFormat: "The type {0} cannot be created in a compute shader (only unmanaged types are supported)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only unmanaged value type objects can be created in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an anonymous object creation expression.
    /// </summary>
    public static readonly DiagnosticDescriptor AnonymousObjectCreationExpression = new(
        id: "CMPW0011",
        title: "Anonymous object creation expression",
        messageFormat: "An anonymous object cannot be created in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An anonymous object cannot be created in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an async modifier on a method or function.
    /// </summary>
    public static readonly DiagnosticDescriptor AsyncModifierOnMethodOrFunction = new(
        id: "CMPW0012",
        title: "Async modifier on method or function",
        messageFormat: "The async modifier cannot be used in methods or functions used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The async modifier cannot be used in methods or functions used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an await expression.
    /// </summary>
    public static readonly DiagnosticDescriptor AwaitExpression = new(
        id: "CMPW0013",
        title: "Await expression",
        messageFormat: "The await expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The await expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a checked expression.
    /// </summary>
    public static readonly DiagnosticDescriptor CheckedExpression = new(
        id: "CMPW0014",
        title: "Checked expression",
        messageFormat: "A checked expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A checked expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a checked statement.
    /// </summary>
    public static readonly DiagnosticDescriptor CheckedStatement = new(
        id: "CMPW0015",
        title: "Checked statement",
        messageFormat: "A checked statement cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A checked statement cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a fixed statement.
    /// </summary>
    public static readonly DiagnosticDescriptor FixedStatement = new(
        id: "CMPW0016",
        title: "Fixed statement",
        messageFormat: "A fixed statement cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A fixed statement cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a foreach statement.
    /// </summary>
    public static readonly DiagnosticDescriptor ForEachStatement = new(
        id: "CMPW0017",
        title: "Foreach statement",
        messageFormat: "A foreach statement cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A foreach statement cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a lock statement.
    /// </summary>
    public static readonly DiagnosticDescriptor LockStatement = new(
        id: "CMPW0018",
        title: "Lock statement",
        messageFormat: "A lock statement cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A lock statement cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a query statement.
    /// </summary>
    public static readonly DiagnosticDescriptor QueryExpression = new(
        id: "CMPW0019",
        title: "Query expression",
        messageFormat: "A LINQ query expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A LINQ query expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a range expression.
    /// </summary>
    public static readonly DiagnosticDescriptor RangeExpression = new(
        id: "CMPW0020",
        title: "Range expression",
        messageFormat: "A range expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A range expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a recursive pattern.
    /// </summary>
    public static readonly DiagnosticDescriptor RecursivePattern = new(
        id: "CMPW0021",
        title: "Recursive pattern",
        messageFormat: "A recursive pattern cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A recursive pattern cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a ref type.
    /// </summary>
    public static readonly DiagnosticDescriptor RefType = new(
        id: "CMPW0022",
        title: "Ref type",
        messageFormat: "A compute shader cannot have a ref type declaration",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot have a ref type declaration.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a relational pattern.
    /// </summary>
    public static readonly DiagnosticDescriptor RelationalPattern = new(
        id: "CMPW0023",
        title: "Relational pattern",
        messageFormat: "A relational pattern cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A relational pattern cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a sizeof expression.
    /// </summary>
    public static readonly DiagnosticDescriptor SizeOfExpression = new(
        id: "CMPW0024",
        title: "Sizeof expression",
        messageFormat: "A sizeof expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A sizeof expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a stackalloc expression.
    /// </summary>
    public static readonly DiagnosticDescriptor StackAllocArrayCreationExpression = new(
        id: "CMPW0025",
        title: "Stackalloc expression",
        messageFormat: "A stackalloc expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A stackalloc expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a throw expression or statement.
    /// </summary>
    public static readonly DiagnosticDescriptor ThrowExpressionOrStatement = new(
        id: "CMPW0026",
        title: "Throw expression or statement",
        messageFormat: "Throw expressions and statements cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Throw expressions and statements cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a try statement.
    /// </summary>
    public static readonly DiagnosticDescriptor TryStatement = new(
        id: "CMPW0027",
        title: "Try statement",
        messageFormat: "A try statement cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A try statement cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a tuple type.
    /// </summary>
    public static readonly DiagnosticDescriptor TupleType = new(
        id: "CMPW0028",
        title: "Tuple type",
        messageFormat: "A compute shader cannot have a tuple type declaration",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot have a tuple type declaration.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a using statement or declaration.
    /// </summary>
    public static readonly DiagnosticDescriptor UsingStatementOrDeclaration = new(
        id: "CMPW0029",
        title: "Using statement or declaration",
        messageFormat: "Using statements and declarations cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Using statements and declarations cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a yield statement.
    /// </summary>
    public static readonly DiagnosticDescriptor YieldStatement = new(
        id: "CMPW0030",
        title: "Yield statement",
        messageFormat: "A yield statement cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A yield statement cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid object declaration.
    /// <para>
    /// Format: <c>"A variable of type {0} cannot be declared in a compute shader"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidObjectDeclaration = new(
        id: "CMPW0031",
        title: "Invalid object declaration",
        messageFormat: "A variable of type {0} cannot be declared in a compute shader (only unmanaged types are supported)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only unmanaged value type objects can be declared in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a pointer type.
    /// </summary>
    public static readonly DiagnosticDescriptor PointerType = new(
        id: "CMPW0032",
        title: "Pointer type",
        messageFormat: "A compute shader cannot have a pointer type declaration",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot have a pointer type declaration.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a function pointer type.
    /// </summary>
    public static readonly DiagnosticDescriptor FunctionPointer = new(
        id: "CMPW0033",
        title: "Function pointer type",
        messageFormat: "A compute shader cannot have a function pointer type declaration",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot have a function pointer type declaration.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an unsafe statement.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsafeStatement = new(
        id: "CMPW0034",
        title: "Unsafe statement",
        messageFormat: "A compute shader cannot have an unsafe statement",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot have an unsafe statement.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an unsafe modifier on a method or function.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsafeModifierOnMethodOrFunction = new(
        id: "CMPW0035",
        title: "Unsafe modifier on method or function",
        messageFormat: "The unsafe modifier cannot be used in methods or functions used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The unsafe modifier cannot be used in methods or functions used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a string literal.
    /// </summary>
    public static readonly DiagnosticDescriptor StringLiteralExpression = new(
        id: "CMPW0036",
        title: "String literal expression",
        messageFormat: "String literal expressions cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "String literal expressions cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an incorrect matrix swizzling property argument.
    /// </summary>
    public static readonly DiagnosticDescriptor NonConstantMatrixSwizzledIndex = new(
        id: "CMPW0037",
        title: "Non constant matrix swizzled property argument",
        messageFormat: "The arguments in a swizzled indexer for a matrix type must be compile-time constants",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The arguments in a swizzled indexer for a matrix type must be compile-time constants.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid shader static field type.
    /// <para>
    /// Format: <c>"The compute shader of type {0} contains or references a static field "{1}" of an invalid type {2} (only primitive, vector and matrix types are supported)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidShaderStaticFieldType = new(
        id: "CMPW0038",
        title: "Invalid shader static field type",
        messageFormat: """The compute shader of type {0} contains or references a static field "{1}" of an invalid type {2} (only primitive, vector and matrix types are supported)""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type representing a compute shader contains or references a static field of a type that is not supported.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid <see cref="DispatchSize"/> usage.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidDispatchSizeUsage = new(
        id: "CMPW0039",
        title: "Invalid DispatchSize usage",
        messageFormat: "The DispatchSize type can only be used within the main body of a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The DispatchSize type can only be used within the main body of a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a property declaration.
    /// <para>
    /// Format: <c>"The compute shader of type {0} contains an invalid property "{1}" declaration (only stateless properties explicitly implementing an interface property can be used)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPropertyDeclaration = new(
        id: "CMPW0040",
        title: "Invalid property declaration",
        messageFormat: """The compute shader of type {0} contains an invalid property "{1}" declaration (only stateless properties explicitly implementing an interface property can be used)""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Property declarations (except for stateless properties explicitly implementing an interface property) cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shader with a root signature that is too large.
    /// <para>
    /// Format: <c>"The compute shader of type {0} has exceeded the maximum allowed size for captured values and resources (the maximum size for the root signature is 64 DWORD constants, but the actual size was {1})"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor ShaderDispatchDataSizeExceeded = new(
        id: "CMPW0041",
        title: "Shader dispatch data size exceeded",
        messageFormat: "The compute shader of type {0} has exceeded the maximum allowed size for captured values and resources (the maximum size for the root signature is 64 DWORD constants, but the actual size was {1})",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot exceed the maximum allowed size for captured values and resources (the maximum size for the root signature is 64 DWORD constants).",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a type implementing multiple shader interfaces.
    /// <para>
    /// Format: <c>"The shader of type {0} cannot implement more than one shader interface"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MultipleShaderTypesImplemented = new(
        id: "CMPW0042",
        title: "Multiple shader implementations for type declaration",
        messageFormat: "The shader of type {0} cannot implement more than one shader interface",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A shader type cannot implement more than one shader interface.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for invalid thread group sizes.
    /// <para>
    /// Format: <c>"The [ThreadGroupSize] attribute on shader type {0} is using invalid thread group size values"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidThreadGroupSizeAttributeValues = new(
        id: "CMPW0044",
        title: "Invalid values for [ThreadGroupSize] attribute",
        messageFormat: "The [ThreadGroupSize] attribute on shader type {0} is using invalid thread group size values",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The thread group sizes for [ThreadGroupSize] have to be in the valid range, and the number of threads in a group cannot exceed the maximum the hardware allows.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for HLSL bytecode shader failed due to a Win32 exception.
    /// <para>
    /// Format: <c>"The shader of type {0} failed to compile due to a Win32 exception (HRESULT: {1:X8}, Message: "{2}")"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor HlslBytecodeFailedWithWin32Exception = new(
        id: "CMPW0045",
        title: "HLSL bytecode compilation failed due to Win32 exception",
        messageFormat: """The shader of type {0} failed to compile due to a Win32 exception (HRESULT: {1:X8}, Message: "{2}")""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The HLSL bytecode for a shader failed to be compiled due to a Win32 exception.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for HLSL bytecode shader failed due to an HLSL compilation exception.
    /// <para>
    /// Format: <c>"The shader of type {0} failed to compile due to an HLSL compiler error (Message: "{1}")"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor HlslBytecodeFailedWithCompilationException = new(
        id: "CMPW0046",
        title: "HLSL bytecode compilation failed due to an HLSL compiler error",
        messageFormat: """The shader of type {0} failed to compile due to an HLSL compiler error (Message: "{1}")""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The HLSL bytecode for a shader failed to be compiled due to an HLSL compiler error.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shader without the thread group size attribute.
    /// <para>
    /// Format: <c>"The shader of type {0} needs to be annotated with [ThreadGroupSize], as dynamic shader compilation is not supported"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingThreadGroupSizeAttribute = new(
        id: "CMPW0047",
        title: "Missing [ThreadGroupSize] attribute on shader type",
        messageFormat: "The shader of type {0} needs to be annotated with [ThreadGroupSize] to be compiled at build time, as dynamic shader compilation is not supported",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All shaders need to be annotated with the [ThreadGroupSize] attribute to be compiled at build time.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for shaders shader with an invalid DefaultThreadGroupSizes value.
    /// <para>
    /// Format: <c>"The [ThreadGroupSize] attribute on shader type {0} is using an invalid DefaultThreadGroupSizes value"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidThreadGroupSizeAttributeDefaultThreadGroupSizes = new(
        id: "CMPW0048",
        title: "Invalid DefaultThreadGroupSizes value for [ThreadGroupSize] use",
        messageFormat: "The [ThreadGroupSize] attribute on shader type {0} is using an invalid DefaultThreadGroupSizes value",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The DefaultThreadGroupSizes value for [ThreadGroupSize] attributes have to be valid.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a method or constructor invocation that is not valid from a shader.
    /// <para>
    /// Format: <c>"The method or constructor {0} cannot be used in a shader (methods or constructors need to either be HLSL intrinsics or with source available for analysis)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMethodOrConstructorCall = new(
        id: "CMPW0049",
        title: "Invalid method or constructor invocation from a shader",
        messageFormat: "The method or constructor {0} cannot be used in a shader (methods or constructors need to either be HLSL intrinsics or with source available for analysis)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Shaders can only invoke methods or constructors that are either HLSL intrinsics or with source available for analysis.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid discovered type.
    /// <para>
    /// Format: <c>"The compute shader or method {0} uses the invalid type {1}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidDiscoveredType = new(
        id: "CMPW0050",
        title: "Invalid discovered type",
        messageFormat: "The compute shader or method {0} uses the invalid type {1} (only some .NET primitive types, HLSL primitive, vector and matrix types, and custom types containing these types can be used, and bool fields in custom struct types have to be replaced with the ComputeWeave.Bool type for alignment reasons)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Shaders and shader methods can only use supported types (some .NET primitive types, HLSL primitive, vector and matrix types, and custom types containing these types can be used, and bool fields in custom struct types have to be replaced with the ComputeWeave.Bool type for alignment reasons).",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a an invalid copy operation for <c>ComputeContext</c>.
    /// <para>
    /// Format: <c>"The compute shader or method {0} uses the invalid type {1}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputeContextCopy = new(
        id: "CMPW0051",
        title: "Invalid ComputeContext copy operation",
        messageFormat: "The ComputeContext type cannot be copied (consider passing it via ref readonly or in instead) and cannot be used as a field of value types (as it could be indirectly copied)",
        category: "ComputeWeave",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The ComputeContext type cannot be copied (and values should rather be passed via ref readonly or in instead) and cannot be used as a field of value types (as it could be indirectly copied).",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when the <c>AllowUnsafeBlocks</c> option is not set.
    /// <para>
    /// Format: <c>"Using [GeneratedComputeShaderDescriptor] requires unsafe blocks being enabled, as they are needed by the source generators to emit valid code (add &lt;AllowUnsafeBlocks&gt;true&lt;/AllowUnsafeBlocks&gt; to your .csproj/.props file)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingAllowUnsafeBlocksOption = new(
        id: "CMPW0052",
        title: "Missing 'AllowUnsafeBlocks' compilation option",
        messageFormat: "Using [GeneratedComputeShaderDescriptor] requires unsafe blocks being enabled, as they are needed by the source generators to emit valid code (add <AllowUnsafeBlocks>true</AllowUnsafeBlocks> to your .csproj/.props file)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Unsafe blocks must be enabled when using [GeneratedComputeShaderDescriptor] for the source generators to emit valid code (the <AllowUnsafeBlocks>true</AllowUnsafeBlocks> option must be set in the .csproj/.props file).",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when a compute shader type doesn't have an associated descriptor.
    /// <para>
    /// Format: <c>"The compute shader of type {0} does not have an associated descriptor (it can be autogenerated via the [GeneratedComputeShaderDescriptor] attribute)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingComputeShaderDescriptorOnComputeShaderType = new(
        id: MissingComputeShaderDescriptorOnComputeShaderTypeId,
        title: "Missing descriptor for compute pixel shader type",
        messageFormat: "The compute shader of type {0} does not have an associated descriptor (it can be autogenerated via the [GeneratedComputeShaderDescriptor] attribute)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "All compute shader types must have an associated descriptor (it can be autogenerated via the [GeneratedComputeShaderDescriptor] attribute).",
        helpLinkUri: "https://github.com/routersys/ComputeWeave",
        customTags: WellKnownDiagnosticTags.CompilationEnd);

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when the <c>[GeneratedComputeShaderDescriptor]</c> attribute is being used on an invalid target type.
    /// <para>
    /// Format: <c>"The type {0} is not a valid target for the [GeneratedComputeShaderDescriptor] attribute (only non generic types implementing the IComputeShader or IComputeShader&lt;TPixel&gt; interface are valid)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGeneratedComputeShaderDescriptorAttributeTarget = new(
        id: "CMPW0054",
        title: "Invalid [GeneratedComputeShaderDescriptor] attribute target",
        messageFormat: "The type {0} is not a valid target for the [GeneratedComputeShaderDescriptor] attribute (only non generic types implementing the IComputeShader or IComputeShader<TPixel> interface are valid)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GeneratedComputeShaderDescriptor] attribute must be used on non generic types that implement the IComputeShader or IComputeShader<TPixel> interfaces.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when the <c>[GeneratedComputeShaderDescriptor]</c> attribute is being used on a type that is not accessible from its containing assembly.
    /// <para>
    /// Format: <c>"The [GeneratedComputeShaderDescriptor] attribute requires target types to be accessible from their containing assembly (type {0} has less effective accessibility than internal)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor NotAccessibleTargetTypeForGeneratedComputeShaderDescriptorAttribute = new(
        id: "CMPW0055",
        title: "Not accessible type using the [GeneratedComputeShaderDescriptor] attribute",
        messageFormat: "The [GeneratedComputeShaderDescriptor] attribute requires target types to be accessible from their containing assembly (type {0} has less effective accessibility than internal)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GeneratedComputeShaderDescriptor] attribute requires target types to be accessible from their containing assembly.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when a field in a type using the <c>[GeneratedComputeShaderDescriptor]</c> attribute has a type that is not accessible from its containing assembly.
    /// <para>
    /// Format: <c>"The [GeneratedComputeShaderDescriptor] attribute requires the type of all fields of target types to be accessible from their containing assembly (type {0} has a field "{1}" of type {2} that has less effective accessibility than internal)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor NotAccessibleFieldTypeInTargetTypeForGeneratedComputeShaderDescriptorAttribute = new(
        id: "CMPW0056",
        title: "Not accessible field type in type using the [GeneratedComputeShaderDescriptor] attribute",
        messageFormat: """The [GeneratedComputeShaderDescriptor] attribute requires the type of all fields of target types to be accessible from their containing assembly (type {0} has a field "{1}" of type {2} that has less effective accessibility than internal)""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GeneratedComputeShaderDescriptor] attribute requires the type of all fields of target types to be accessible from their containing assembly.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when a shader type with any fields is not readonly.
    /// <para>
    /// Format: <c>"The shader of type {0} is not readonly (shaders cannot mutate their instance state while running, so shader types not being readonly makes them error prone)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor NotReadOnlyShaderType = new(
        id: "CMPW0057",
        title: "Not readonly shader type (using IComputeShader or IComputeShader<T>)",
        messageFormat: "The shader of type {0} is not readonly (shaders cannot mutate their instance state while running, so shader types not being readonly makes them error prone)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Shader types should be readonly (shaders cannot mutate their instance state while running, so shader types not being readonly makes them error prone).",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for when a field annotated with <c>[GloballyCoherent]</c> is not valid.
    /// <para>
    /// Format: <c>"The field "{0}" is annotated with [GloballyCoherent], but is not a valid target for it (only ReadWriteBuffer&lt;T&gt; instance fields in compute shader types can be annotated with [GloballyCoherent])"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGloballyCoherentFieldDeclaration = new(
        id: "CMPW0058",
        title: "Invalid [GloballyCoherent] field declaration",
        messageFormat: """The field "{0}" is annotated with [GloballyCoherent], but is not a valid target for it (only ReadWriteBuffer<T> instance fields in compute shader types can be annotated with [GloballyCoherent])""",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [GloballyCoherent] attribute is only valid on ReadWriteBuffer<T> instance fields in compute shader types.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an initializer expression.
    /// </summary>
    public static readonly DiagnosticDescriptor InitializerExpression = new(
        id: "CMPW0059",
        title: "Initializer expression",
        messageFormat: "An initializer expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An initializer expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a collection expression.
    /// </summary>
    public static readonly DiagnosticDescriptor CollectionExpression = new(
        id: "CMPW0060",
        title: "Collection expression",
        messageFormat: "A collection expression cannot be used in a compute shader",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A collection expression cannot be used in a compute shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a constructor with a base constructor declaration.
    /// <para>
    /// Format: <c>"The constructor {0} has a base constructor declaration, which cannot be used in a shader (only standalone constructors are allowed)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidBaseConstructorDeclaration = new(
        id: "CMPW0061",
        title: "Invalid base constructor declaration",
        messageFormat: "The constructor {0} has a base constructor declaration, which cannot be used in a shader (only standalone constructors are allowed)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Only standalone constructors (with no base constructor declaration) can be used in a shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a <see langword="this"/> expression.
    /// </summary>
    public static readonly DiagnosticDescriptor ThisExpression = new(
        id: "CMPW0062",
        title: "Invalid 'this' expression",
        messageFormat: "A compute shader cannot use a 'this' expression outside of member accesses (such as 'this.field')",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute shader cannot use a 'this' expression outside of member accesses (such as 'this.field').",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invocation of a <c>Math</c> or <c>MathF</c> API.
    /// <para>
    /// Format: <c>"The method {0} cannot be used in a shader, use equivalent APIs from the Hlsl type instead"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidMathOrMathFCall = new(
        id: "CMPW0063",
        title: "Invalid Math or MathF invocation from a shader",
        messageFormat: "The method {0} cannot be used in a shader, use equivalent APIs from the Hlsl type instead",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Methods from the Math and MathF types cannot be used in a shader, and equivalent APIs from the Hlsl type should be used instead.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shader missing [RequiresDoublePrecisionSupport].
    /// <para>
    /// Format: <c>"The shader {0} requires double precision support, but it does not have the [RequiresDoublePrecisionSupport] attribute on it (adding the attribute is necessary to explicitly opt-in to that functionality)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingRequiresDoublePrecisionSupportAttribute = new(
        id: "CMPW0064",
        title: "Missing [RequiresDoublePrecisionSupport] attribute",
        messageFormat: "The shader {0} requires double precision support, but it does not have the [RequiresDoublePrecisionSupport] attribute on it (adding the attribute is necessary to explicitly opt-in to that functionality)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Shaders performing double precision operations must be annotated with [RequiresDoublePrecisionSupport] to explicitly opt-in to that functionality.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shader is unnecessarily using [RequiresDoublePrecisionSupportAttribute].
    /// <para>
    /// Format: <c>"The shader {0} does not require double precision support, but it has the [RequiresDoublePrecisionSupport] attribute on it (using the attribute is not needed if the shader is not performing any double precision operations)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnnecessaryRequiresDoublePrecisionSupportAttribute = new(
        id: "CMPW0065",
        title: "Unnecessary [RequiresDoublePrecisionSupport] attribute",
        messageFormat: "The shader {0} does not require double precision support, but it has the [RequiresDoublePrecisionSupport] attribute on it (using the attribute is not needed if the shader is not performing any double precision operations)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Shaders not performing any double precision operations should not be annotated with [RequiresDoublePrecisionSupport], as the attribute is not needed in that case.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a compute pipeline host type that is not a sealed partial class.
    /// <para>
    /// Format: <c>"The type {0} annotated with [ComputePipelineHost] must be a sealed partial class"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputePipelineHostType = new(
        id: "CMPW0066",
        title: "Invalid compute pipeline host type",
        messageFormat: "The type {0} annotated with [ComputePipelineHost] must be a sealed partial class",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type annotated with [ComputePipelineHost] must be a sealed partial class.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid maximum concurrent invocations value.
    /// <para>
    /// Format: <c>"The [ComputePipelineHost] attribute on type {0} must specify a maximum concurrent invocations value greater than or equal to 1"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputePipelineHostMaximumConcurrentInvocations = new(
        id: "CMPW0068",
        title: "Invalid maximum concurrent invocations value",
        messageFormat: "The [ComputePipelineHost] attribute on type {0} must specify a maximum concurrent invocations value greater than or equal to 1",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The [ComputePipelineHost] attribute must specify a maximum concurrent invocations value greater than or equal to 1.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a compute interop resource set type that is not a sealed partial class.
    /// <para>
    /// Format: <c>"The type {0} annotated with [ComputeInteropResourceSet] must be a sealed partial class"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputeInteropResourceSetType = new(
        id: "CMPW0074",
        title: "Invalid compute interop resource set type",
        messageFormat: "The type {0} annotated with [ComputeInteropResourceSet] must be a sealed partial class",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type annotated with [ComputeInteropResourceSet] must be a sealed partial class.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a compute resource group type that is not a sealed partial class.
    /// <para>
    /// Format: <c>"The type {0} annotated with [ComputeResourceGroup] must be a sealed partial class and cannot be a struct"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputeResourceGroupType = new(
        id: "CMPW0100",
        title: "Invalid compute resource group type",
        messageFormat: "The type {0} annotated with [ComputeResourceGroup] must be a sealed partial class and cannot be a struct",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type annotated with [ComputeResourceGroup] must be a sealed partial class and cannot be a struct.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a generic or non-partially-nested compute pipeline container type.
    /// <para>
    /// Format: <c>"The type {0} annotated with [{1}] cannot be generic and must have all its containing types declared as partial"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputePipelineContainerType = new(
        id: "CMPW0106",
        title: "Invalid compute pipeline container type",
        messageFormat: "The type {0} annotated with [{1}] cannot be generic and must have all its containing types declared as partial",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A type annotated with [ComputePipelineHost], [ComputeInteropResourceSet] or [ComputeResourceGroup] cannot be generic and must have all its containing types declared as partial.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shared texture field not declaring a ReadWrite compute access.
    /// <para>
    /// Format: <c>"The shared texture field {0} must declare a compute access of ReadWrite"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputeSharedTextureComputeAccess = new(
        id: "CMPW0076",
        title: "Invalid shared texture compute access",
        messageFormat: "The shared texture field {0} must declare a compute access of ReadWrite",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A field annotated with [ComputeSharedTexture] must declare a compute access of ReadWrite, as it is bound to a shader as a ReadWriteTexture2D.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a shared texture field with an invalid declaration.
    /// <para>
    /// Format: <c>"The shared texture field {0} has an invalid declaration (it must be a private readonly instance field of type SharedTextureSlot&lt;T, TPixel, TView&gt; without an initializer)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputeSharedTextureFieldDeclaration = new(
        id: "CMPW0109",
        title: "Invalid shared texture field declaration",
        messageFormat: "The shared texture field {0} has an invalid declaration (it must be a private readonly instance field of type SharedTextureSlot<T, TPixel, TView> without an initializer)",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A field annotated with [ComputeSharedTexture] must be a private readonly instance field of type SharedTextureSlot<T, TPixel, TView> and must not have an initializer, as the runtime binds the slot to the interop resource set it publishes generations into.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a compute pipeline method with an invalid signature.
    /// <para>
    /// Format: <c>"The compute pipeline method {0} has an invalid signature (it must return void, take an 'in ComputeContext' as its first parameter, and only declare value or 'in' parameters otherwise)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputePipelineMethodSignature = new(
        id: "CMPW0069",
        title: "Invalid compute pipeline method signature",
        messageFormat: "The compute pipeline method {0} has an invalid signature (it must return void, take an 'in ComputeContext' as its first parameter, and only declare value or 'in' parameters otherwise)",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute pipeline method must return void, take an 'in ComputeContext' as its first parameter, and only declare value or 'in' parameters otherwise.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a compute pipeline method with an unsupported form.
    /// <para>
    /// Format: <c>"The compute pipeline method {0} has an unsupported form (it cannot be static, generic, async or an iterator)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedComputePipelineMethodForm = new(
        id: "CMPW0091",
        title: "Unsupported compute pipeline method form",
        messageFormat: "The compute pipeline method {0} has an unsupported form (it cannot be static, generic, async or an iterator)",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute pipeline method cannot be static, generic, async or an iterator.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a graphics resource parameter missing [ComputeResource].
    /// <para>
    /// Format: <c>"The parameter {0} of a compute pipeline method is a graphics resource and must be annotated with [ComputeResource]"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingComputeResourceAttribute = new(
        id: "CMPW0070",
        title: "Missing [ComputeResource] attribute",
        messageFormat: "The parameter {0} of a compute pipeline method is a graphics resource and must be annotated with [ComputeResource]",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A graphics resource parameter of a compute pipeline method must be annotated with [ComputeResource].",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a compute interop method without any external resource parameter.
    /// <para>
    /// Format: <c>"The compute interop method {0} must declare at least one parameter annotated with [ComputeResource] using an external sharing"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingExternalComputeResourceInInteropMethod = new(
        id: "CMPW0072",
        title: "Missing external resource in compute interop method",
        messageFormat: "The compute interop method {0} must declare at least one parameter annotated with [ComputeResource] using an external sharing",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute interop method must declare at least one parameter annotated with [ComputeResource] using an external sharing.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a user-declared generated lifecycle member.
    /// <para>
    /// Format: <c>"The type {0} cannot declare the member {1}, as it is generated for compute pipeline hosts and interop resource sets"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGeneratedLifecycleMemberDeclaration = new(
        id: "CMPW0095",
        title: "Invalid generated lifecycle member declaration",
        messageFormat: "The type {0} cannot declare the member {1}, as it is generated for compute pipeline hosts and interop resource sets",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute pipeline host or interop resource set cannot declare an instance constructor, a finalizer, a Dispose() method or a WaitForDisposal() method, as those are generated.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invalid compute pipeline host device field.
    /// <para>
    /// Format: <c>"The compute pipeline host {0} must declare a 'private readonly GraphicsDevice' field named {1} with no initializer"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidComputePipelineHostDeviceField = new(
        id: "CMPW0067",
        title: "Invalid compute pipeline host device field",
        messageFormat: "The compute pipeline host {0} must declare a 'private readonly GraphicsDevice' field named {1} with no initializer",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute pipeline host must declare a 'private readonly GraphicsDevice' field with the configured name and no initializer.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned slot declaring a resource type its access contract cannot produce.
    /// <para>
    /// Format: <c>"The owned slot {0} declares the resource type {1}, which cannot hold the {2} instance its access contract produces"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOwnedSlotResourceType = new(
        id: "CMPW0094",
        title: "Invalid owned slot declaration",
        messageFormat: "The owned slot {0} declares the resource type {1}, which cannot hold the {2} instance its access contract produces",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The managed resource type of an owned slot is determined by its access contract, so the declared resource type must be able to hold it.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a resource group member declaring a resource type its access contract cannot produce.
    /// <para>
    /// Format: <c>"The resource group member {0} declares the resource type {1}, which cannot hold the {2} instance its access contract produces"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidResourceGroupMemberResourceType = new(
        id: "CMPW0107",
        title: "Invalid resource group property contract",
        messageFormat: "The resource group member {0} declares the resource type {1}, which cannot hold the {2} instance its access contract produces",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The managed resource type of a resource group member is determined by its access contract, so the declared resource type must be able to hold it.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned member with a conflicting generated plan signature.
    /// <para>
    /// Format: <c>"The owned member {0} of {1} must have a canonical name that is not empty and that does not conflict with another owned member or with a declared member"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGeneratedPlanSignature = new(
        id: "CMPW0104",
        title: "Generated plan signature conflict",
        messageFormat: "The owned member {0} of {1} must have a canonical name that is not empty and that does not conflict with another owned member or with a declared member",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The canonical name of an owned member must not be empty, and the plan members generated from it must not conflict with another declared or generated member.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned resource that is not declared through a slot.
    /// <para>
    /// Format: <c>"The owned resource {0} declares the type {1}, but an owned resource must be declared as a ComputeResourceSlot&lt;T&gt; or a ComputeResourceGroupSlot&lt;T&gt; field"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOwnedResourceSlotDeclaration = new(
        id: "CMPW0075",
        title: "Owned resource is not declared through a slot",
        messageFormat: "The owned resource {0} declares the type {1}, but an owned resource must be declared as a ComputeResourceSlot<T> or a ComputeResourceGroupSlot<T> field",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An owned resource is created and replaced by its slot, so it must be declared as a ComputeResourceSlot<T> or a ComputeResourceGroupSlot<T> field.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned slot that declares no recovery contract.
    /// <para>
    /// Format: <c>"The owned slot {0} declares no recovery contract, which is required to recover its contents after a replacement"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MissingOwnedResourceRecoveryContract = new(
        id: "CMPW0101",
        title: "Missing owned resource recovery contract",
        messageFormat: "The owned slot {0} declares no recovery contract, which is required to recover its contents after a replacement",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An owned slot recreates its resource on every replacement, so it must declare how the contents of the previous generation are recovered.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a resource group member that declares a recovery contract.
    /// <para>
    /// Format: <c>"The resource group member {0} declares a recovery contract, which belongs to the slot holding the group"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateResourceGroupMemberRecoveryContract = new(
        id: "CMPW0108",
        title: "Duplicate resource group member recovery contract",
        messageFormat: "The resource group member {0} declares a recovery contract, which belongs to the slot holding the group",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A recovery contract is declared once per slot, so the members of a resource group must not declare one.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a raw external view escaping the scope it is valid in.
    /// <para>
    /// Format: <c>"The raw view returned by {0} is only valid within the scope it was obtained in, so it cannot be stored or returned"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor RawExternalViewEscape = new(
        id: "CMPW0096",
        title: "Raw external view escape",
        messageFormat: "The raw view returned by {0} is only valid within the scope it was obtained in, so it cannot be stored or returned",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A raw external view is released with the lease or the borrow it was obtained from, so it must not outlive that scope.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a transient CPU upload performed inside a compute pipeline.
    /// <para>
    /// Format: <c>"The compute pipeline method {0} uploads from CPU memory through {1}, which allocates a transient upload resource"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedTransientCpuUploadInPipeline = new(
        id: "CMPW0105",
        title: "Unsupported transient CPU upload in a compute pipeline",
        messageFormat: "The compute pipeline method {0} uploads from CPU memory through {1}, which allocates a transient upload resource",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute pipeline records GPU work without allocating, so uploading from CPU memory has to happen through a manual compute context or an owned upload resource.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a member declaring more than one contract attribute.
    /// <para>
    /// Format: <c>"The member {0} declares both [ComputePipelineResource] and [ComputeSharedTexture], which are exclusive contracts"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateContractAttribute = new(
        id: "CMPW0089",
        title: "Duplicate contract attribute",
        messageFormat: "The member {0} declares both [ComputePipelineResource] and [ComputeSharedTexture], which are exclusive contracts",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A member declares exactly one contract, so it cannot carry more than one contract attribute.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a pipeline method whose generated members conflict with a declared member.
    /// <para>
    /// Format: <c>"The compute pipeline method {0} of {1} must have a canonical name that is not empty, and the overload and invocation type generated from it must not conflict with another declared or generated member"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGeneratedPipelineOverload = new(
        id: "CMPW0073",
        title: "Generated pipeline overload conflict",
        messageFormat: "The compute pipeline method {0} of {1} must have a canonical name that is not empty, and the overload and invocation type generated from it must not conflict with another declared or generated member",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A compute pipeline method generates a submitting overload and a nested invocation type, so neither of them can conflict with another declared or generated member.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a pipeline parameter bound as read-write without a read-write access.
    /// <para>
    /// Format: <c>"The pipeline parameter {0} declares the type {1}, which is bound to a shader as read-write, so its declared access must be read-write"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidReadWriteParameterAccessContract = new(
        id: "CMPW0098",
        title: "Invalid read-write parameter access contract",
        messageFormat: "The pipeline parameter {0} declares the type {1}, which is bound to a shader as read-write, so its declared access must be read-write",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A resource bound to a shader as a read-write type is written by the shader, so the access it declares must be read-write.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an internal resource that is not a graphics resource.
    /// <para>
    /// Format: <c>"The internal resource {0} declares the type {1}, which is not a graphics resource"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidInternalResourceContract = new(
        id: "CMPW0071",
        title: "Invalid internal resource contract",
        messageFormat: "The internal resource {0} declares the type {1}, which is not a graphics resource",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An internal resource of a compute pipeline host is bound as a graphics resource, so its declared type must be one.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned slot that is assigned instead of created in place.
    /// <para>
    /// Format: <c>"The owned slot {0} must be initialized with an object creation expression, not with an assigned value"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOwnedSlotInitializer = new(
        id: "CMPW0087",
        title: "Invalid owned slot initializer",
        messageFormat: "The owned slot {0} must be initialized with an object creation expression, not with an assigned value",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An owned slot has to exist before the generated host factory binds it, so it must be created in its own declaration.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a resource declared through a collection type.
    /// <para>
    /// Format: <c>"The resource {0} declares the collection type {1}, which cannot be a compute pipeline resource"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedDynamicResourceCollection = new(
        id: "CMPW0092",
        title: "Unsupported dynamic resource collection",
        messageFormat: "The resource {0} declares the collection type {1}, which cannot be a compute pipeline resource",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The resources of a compute pipeline are declared one by one, so a dynamic resource collection cannot be declared.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned resource whose type has no resource plan.
    /// <para>
    /// Format: <c>"The owned resource {0} declares the type {1}, which has no resource plan dimensions"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedResourcePlanMember = new(
        id: "CMPW0102",
        title: "Unsupported resource plan member",
        messageFormat: "The owned resource {0} declares the type {1}, which has no resource plan dimensions",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An owned resource is created from an exact resource plan, so its type must be a buffer or a 2D texture.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a pipeline host that owns a disposable field other than a slot.
    /// <para>
    /// Format: <c>"The compute pipeline host {0} declares the owned disposable field {1}, which it cannot release"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedOwnedDisposableFieldInPipelineHost = new(
        id: "CMPW0097",
        title: "Unsupported owned disposable field in a compute pipeline host",
        messageFormat: "The compute pipeline host {0} declares the owned disposable field {1}, which it cannot release",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The generated disposal of a compute pipeline host only releases its owned slots, so the host must not declare any other owned disposable field.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned resource parameter with an invalid declaration.
    /// <para>
    /// Format: <c>"The parameter {0} does not declare a valid owned resource contract"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOwnedResourceParameterDeclaration = new(
        id: "CMPW0110",
        title: "Invalid owned resource parameter declaration",
        messageFormat: "The parameter {0} does not declare a valid owned resource contract",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A parameter annotated with [ComputeOwnedResource] must belong to a method annotated with [ComputePipeline], must not declare another resource contract, and must name an owned resource slot field declared by the host.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an owned resource parameter that does not declare the type of its slot.
    /// <para>
    /// Format: <c>"The parameter {0} declares the type {1}, but the owned resource slot {2} provides {3}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOwnedResourceParameterType = new(
        id: "CMPW0111",
        title: "Invalid owned resource parameter type",
        messageFormat: "The parameter {0} declares the type {1}, but the owned resource slot {2} provides {3}",
        category: "ComputeWeave.Pipelines",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A parameter annotated with [ComputeOwnedResource] receives the resource owned by a ComputeResourceSlot<T>, or the resource group owned by a ComputeResourceGroupSlot<TGroup>, so it must declare that type argument.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an intrinsic that a compute shader cannot use.
    /// <para>
    /// Format: <c>"The intrinsic {0} cannot be used in a compute shader, because {1}"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedHlslIntrinsicInvocation = new(
        id: "CMPW0112",
        title: "Unsupported HLSL intrinsic invocation",
        messageFormat: "The intrinsic {0} cannot be used in a compute shader, because {1}",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Some intrinsics declared by the Hlsl type target a rasterization stage or a shader model that the compute shaders compiled by ComputeWeave do not provide, so they cannot be used in a shader.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a local function that is not static.
    /// </summary>
    public static readonly DiagnosticDescriptor NonStaticLocalFunction = new(
        id: "CMPW0113",
        title: "Non static local function",
        messageFormat: "Local functions used in a compute shader have to be declared static",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Local functions used in a compute shader are lifted to top level HLSL functions, and HLSL has no closures, so only a static local function can be translated. A local function in a shader cannot read instance members either, because C# does not allow a local function in a struct to capture this.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a property access with no HLSL mapping.
    /// <para>
    /// Format: <c>"The property {0} cannot be accessed in a compute shader (only properties that map to an HLSL intrinsic are available, so a method or a field has to be used instead)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPropertyAccess = new(
        id: "CMPW0114",
        title: "Invalid property access",
        messageFormat: "The property {0} cannot be accessed in a compute shader (only properties that map to an HLSL intrinsic are available, so a method or a field has to be used instead)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HLSL structs carry fields and no properties, so only a property with an HLSL mapping can be translated. A property declared on a custom type is left out of the generated struct, and without this diagnostic the access is written out as it stands and fails in the HLSL compiler instead, naming generated code the author never wrote.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");
    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an operator with no HLSL mapping.
    /// <para>
    /// Format: <c>"The operator {0} cannot be used in a compute shader (only the operators declared on the HLSL primitive types are translated, so the operation has to be written as a method call or over the fields directly)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOperatorUse = new(
        id: "CMPW0115",
        title: "Invalid operator use",
        messageFormat: "The operator {0} cannot be used in a compute shader (only the operators declared on the HLSL primitive types are translated, so the operation has to be written as a method call or over the fields directly)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An operator declared on a custom type is not imported into the generated HLSL, so the body the author wrote never runs. Most forms then fail in the HLSL compiler, but a conversion between a struct and a scalar is one HLSL performs on its own, taking the first member or filling every member, so without this diagnostic the shader silently computes a different value.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an element access that HLSL does not provide.
    /// <para>
    /// Format: <c>"The element access on {0} cannot be used in a compute shader (only an element access that HLSL itself provides is translated, and an indexer declared in source is not imported, so the element has to be reached through a field or a method instead)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidElementAccess = new(
        id: "CMPW0116",
        title: "Invalid element access",
        messageFormat: "The element access on {0} cannot be used in a compute shader (only an element access that HLSL itself provides is translated, and an indexer declared in source is not imported, so the element has to be reached through a field or a method instead)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HLSL provides an element access on its own vector and matrix types, on the resource types and on an array, and on nothing else. An indexer declared in source is not imported, so the accessor the author wrote never runs. Most of these accesses then fail in the HLSL compiler, naming a type it never saw, but an extension indexer over a type HLSL can index resolves to the built-in element access instead and the shader silently computes a different value. An inline array is reported the same way, its element access resolving through a span the author never wrote.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invocation of a generic method.
    /// <para>
    /// Format: <c>"The method {0} cannot be called in a compute shader (HLSL has no type parameters, so a generic method cannot be translated and the method has to be declared for the concrete type instead)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidGenericMethodCall = new(
        id: "CMPW0117",
        title: "Invalid generic method call",
        messageFormat: "The method {0} cannot be called in a compute shader (HLSL has no type parameters, so a generic method cannot be translated and the method has to be declared for the concrete type instead)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "HLSL has no type parameters, so a generic method is neither mapped to an intrinsic nor importable: rewriting its declaration carries the type parameter list into the generated source. Without this diagnostic that declaration reaches the HLSL compiler, which reports it under a generated name the author never wrote.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a size accessor read on a constant buffer.
    /// <para>
    /// Format: <c>"The property {0} cannot be accessed on a constant buffer in a compute shader (a constant buffer is written to HLSL as the value it holds and not as a resource, so it has no dimensions to query)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidConstantBufferSizeAccess = new(
        id: "CMPW0118",
        title: "Invalid constant buffer size access",
        messageFormat: "The property {0} cannot be accessed on a constant buffer in a compute shader (a constant buffer is written to HLSL as the value it holds and not as a resource, so it has no dimensions to query)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A constant buffer becomes the value it holds in the generated HLSL, so it carries none of the dimension queries a resource has. Without this diagnostic the read is written out as it stands, and when a structured buffer in the same shader has already claimed the accessor the two types share, it is written out instead as a call to a generated helper that does not accept it, naming generated code the author never wrote.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an invocation of a C# extension member.
    /// <para>
    /// Format: <c>"The method {0} cannot be called in a compute shader (it is declared in an extension block, which is not imported into the generated HLSL, so the body it declares would never run)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidExtensionMemberCall = new(
        id: "CMPW0119",
        title: "Invalid extension member call",
        messageFormat: "The method {0} cannot be called in a compute shader (it is declared in an extension block, which is not imported into the generated HLSL, so the body it declares would never run)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A member declared in an extension block belongs to a type the author cannot name, and the import path only reaches a static method or an instance method on a struct, so the declaration is never rewritten. Without this diagnostic the call is written out as it stands and fails in the HLSL compiler, naming a member it never saw. An extension method declared with a 'this' parameter is imported and is unaffected, as is a static method declared inside an extension block, which belongs to the enclosing static class.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for the construction of a type declaring a primary constructor.
    /// <para>
    /// Format: <c>"The type {0} cannot be constructed in a compute shader (it declares a primary constructor, and the way its captured parameters reach the members of the type cannot be tracked while preserving the same semantics, so an explicit constructor has to be declared instead)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidPrimaryConstructorUse = new(
        id: "CMPW0120",
        title: "Invalid primary constructor use",
        messageFormat: "The type {0} cannot be constructed in a compute shader (it declares a primary constructor, and the way its captured parameters reach the members of the type cannot be tracked while preserving the same semantics, so an explicit constructor has to be declared instead)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A primary constructor is refused on purpose, and not for want of source. Its parameters are captured, and a capture can be reached from any member of the type in ways the rewriting cannot follow while preserving the same semantics. The shader type's own primary constructor is unaffected, its captures becoming the shader fields. Without this diagnostic the refusal is reported as a constructor with no source to analyze, which the author cannot act on, the source being right there.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for syntax outside the set a shader body may use.
    /// <para>
    /// Format: <c>"The C# syntax {0} is not in the set a shader body may use"</c>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// The set is measured rather than designed, so a kind outside it is one the set records no verdict for.
    /// That is not the same as one nothing has judged: a kind the rewriter always refuses is outside the set
    /// as well, because refusing it keeps it out of what the measurement sees, and such a kind is answered by
    /// its own refusal rather than by this. The severity was raised once the whole solution was built with it
    /// and no shader reported it, and once every construct measured to work was shown to be in the set.
    /// </remarks>
    public static readonly DiagnosticDescriptor UnknownShaderSyntax = new(
        id: "CMPW0121",
        title: "Shader syntax outside the accepted set",
        messageFormat: "The C# syntax {0} is not in the set a shader body may use",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The set of C# syntax a shader body may use is measured, not designed: it is what the rewriter walks when this repository is built, plus the constructs that were built one at a time and shown to compute the same value on a device. Syntax outside it is syntax the set records no verdict for, so writing it into HLSL would mean translating a construct nothing has decided anything about. Some of it compiles and computes the right value, and some of it reaches the HLSL compiler and fails there, naming generated code the author never wrote. The two were told apart by measurement while this was recorded rather than refused, and every construct that was shown to work is in the set, so what is left outside it is refused here instead, against the source the author wrote. A construct with a refusal of its own is answered by that refusal alone, this one being dropped beside it so that one cause names one place. The set grows by measurement rather than by design, so a construct that HLSL can express and that computes the same value belongs in it, and an issue naming one is how it gets there.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a local function that declares type parameters.
    /// <para>
    /// Format: <c>"The local function {0} cannot be declared in a compute shader (HLSL has no type parameters, so a generic local function cannot be translated and it has to be declared for the concrete type instead)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor GenericLocalFunction = new(
        id: "CMPW0122",
        title: "Generic local function",
        messageFormat: "The local function {0} cannot be declared in a compute shader (HLSL has no type parameters, so a generic local function cannot be translated and it has to be declared for the concrete type instead)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A local function is lifted to a top level HLSL function, which carries its type parameter list with it. HLSL has no type parameters, so the lifted function does not compile. The declaration is refused rather than the call, because a local function that is never called is lifted just the same.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an intrinsic with an out parameter that is given a matrix the shader compiler terminates on.
    /// <para>
    /// Format: <c>"The intrinsic {0} cannot be given {1} in a compute shader (the shader compiler terminates on that combination, so the call is refused before it runs)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor MatrixOnIntrinsicWithOutParameter = new(
        id: "CMPW0123",
        title: "Matrix given to an intrinsic with an out parameter",
        messageFormat: "The intrinsic {0} cannot be given {1} in a compute shader (the shader compiler terminates on that combination, so the call is refused before it runs)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The shader compiler terminates with an access violation on two combinations of a matrix and an intrinsic that writes through an out parameter. One out parameter terminates on an integer matrix, which was measured on modf, frexp and sincos alike. Two of them terminate on a matrix of any element type, which was measured on sincos, the only intrinsic declaring two. A floating point matrix given to modf or frexp compiles, and so does a vector or a scalar given to any of them, so none of those is refused. Without the refusal the build fails with a native fatal error that names no source line, because the compiler is gone before it can report one. Direct2D shaders are not affected: they are compiled through FXC, which does not have the defect.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a static field initializer reaching the field it initializes.
    /// <para>
    /// Format: <c>"The static field {0} is initialized from an expression that reads it back, directly or through a declaration that initializer reaches"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor CyclicStaticFieldInitializer = new(
        id: "CMPW0124",
        title: "Static field initializer reaching the field it initializes",
        messageFormat: "The static field {0} is initialized from an expression that reads it back, directly or through a declaration that initializer reaches",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "C# runs the initializer once, and where the cycle closes it reads the field as its default value, so the field holds whatever that produces. HLSL has no defined order for global static initializers, so the value the shader computes is not the one C# computes, and the shader compiler accepts the source without saying anything: the generated HLSL was measured to compile with the cycle written out as it stands. Before this report the generator faulted instead, adding the same key to the collection of static field definitions twice, which discards the descriptors for every shader in the compilation unit and leaves the author with errors that name none of this.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for an operation whose operands C# widens past the HLSL type set.
    /// <para>
    /// Format: <c>"The operands of this operation are widened to {0}, which is outside the HLSL type set (a signed and an unsigned integer in one operation are widened together, and that widening cannot be written into the generated code, so the operands have to be brought to one type with an explicit conversion)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidOperandWidening = new(
        id: "CMPW0125",
        title: "Operands widened past the HLSL type set",
        messageFormat: "The operands of this operation are widened to {0}, which is outside the HLSL type set (a signed and an unsigned integer in one operation are widened together, and that widening cannot be written into the generated code, so the operands have to be brought to one type with an explicit conversion)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "C# brings the operands of an operation to a common type before performing it, and a signed integer beside an unsigned one brings both to a 64 bit integer, which the HLSL type set has no name for. That conversion cannot be written into the generated code, which holds the operands as they stand, so the shader compiler resolves the operation over them instead and the unsigned kind wins: a comparison answers the other way and an arithmetic result wraps at 32 bits. Neither compiler reports anything, so without this the shader silently computes a different value. The judgment is over the type the operands are widened to rather than over the pair of kinds that reached it, so any pair widening past the set is refused alike.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a variable a static field initializer would have to declare.
    /// <para>
    /// Format: <c>"The variable {0} cannot be declared in a static field initializer (a shader body declares it ahead of the call that writes to it, and HLSL writes an initializer as one expression with nothing ahead of it)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor VariableDeclaredInStaticFieldInitializer = new(
        id: "CMPW0126",
        title: "Variable declared in a static field initializer",
        messageFormat: "The variable {0} cannot be declared in a static field initializer (a shader body declares it ahead of the call that writes to it, and HLSL writes an initializer as one expression with nothing ahead of it)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "An out argument written as a declaration and a discarded one both need a variable the rewriting introduces, which a shader body declares at the start of the body and passes to the call. A static field initializer is written as one HLSL expression, so there is nowhere ahead of it to put that declaration. Giving the variable a global static of its own instead would share one storage across every invocation of the shader, and HLSL leaves the order of global static initializers undefined, so the field could read a value C# never computes. Without this the declaration is written into the call as it stands, and the shader compiler answers by naming generated code the author never wrote.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");

    /// <summary>
    /// Gets a <see cref="DiagnosticDescriptor"/> for a declaration that carries no body.
    /// <para>
    /// Format: <c>"The declaration of {0} cannot be used in a compute shader (it carries no body, so there is nothing to write into the generated HLSL)"</c>.
    /// </para>
    /// </summary>
    public static readonly DiagnosticDescriptor DeclarationWithNoBody = new(
        id: "CMPW0127",
        title: "Declaration with no body",
        messageFormat: "The declaration of {0} cannot be used in a compute shader (it carries no body, so there is nothing to write into the generated HLSL)",
        category: "ComputeWeave.Shaders",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "What is written into the generated HLSL is built from the body of the declaration, so one that carries none has nothing to write. An extern declaration is that case, and C# reports only a warning for it. Without this a method or a local function is written out as a declaration with no body and the shader compiler answers by naming generated code the author never wrote, while a constructor and the entry point end the generator instead, which discards the descriptors for every shader in the compilation unit. What is reported follows what is written out: a member of an external type is written out where the shader reaches it, and one it never reaches is left alone. A declaration split into parts is unaffected, the implementing part being the one that is read.",
        helpLinkUri: "https://github.com/routersys/ComputeWeave");
}