﻿using JetBrains.Annotations;

namespace ES.FX.Additions.OneOf.Types;

[PublicAPI]
public record struct Fatal;

[PublicAPI]
public record struct Fatal<T>(T Value);