﻿using JetBrains.Annotations;

namespace ES.FX.OneOf.Types;

[PublicAPI]
public record struct Failure;

[PublicAPI]
public record struct Failure<T>(T Value);