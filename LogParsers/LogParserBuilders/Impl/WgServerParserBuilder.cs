﻿using System;
using System.Collections.Generic;

namespace LogParsers
{
    /// <summary>
    /// Contains the mapping context between files within the "wgserver" directory in the logs and their associated parsers.
    /// </summary>
    internal sealed class WgServerParserBuilder : BaseParserBuilder, IParserBuilder
    {
        private static readonly IDictionary<string, Type> fileMap =
            new Dictionary<string, Type>
            {
                { @"^wgserver.*log.*", typeof(WgServerJavaParser) }
            };

        protected override IDictionary<string, Type> FileMap
        {
            get { return fileMap; }
        }
    }
}