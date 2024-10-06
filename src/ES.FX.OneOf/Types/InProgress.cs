﻿using JetBrains.Annotations;

namespace ES.FX.OneOf.Types;

[PublicAPI]
public record struct InProgress;

[PublicAPI]
public record struct InProgress<T>(T Value);