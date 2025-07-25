﻿// Copyright (c) Martin Costello, 2022. All rights reserved.
// Licensed under the Apache 2.0 license. See the LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Authorization;

namespace MartinCostello.Costellobot.Authorization;

/// <summary>
/// A class representing the authorization requirement for administrators.
/// </summary>
public sealed class AdministratorRequirement : IAuthorizationRequirement;
