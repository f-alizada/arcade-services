// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.ApiVersioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Maestro.Web.Api.v2020_02_20.Controllers;

/// <summary>
///   We don't use Release Pipelines anymore.
/// </summary>
[Route("pipelines")]
[ApiVersion("2020-02-20")]
[AllowAnonymous]
public class PipelinesController
{
}
