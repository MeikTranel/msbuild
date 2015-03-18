﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for TaskCommandLineEventArgs</summary>
//-----------------------------------------------------------------------

using System;

using Microsoft.Build.Framework;
using NUnit.Framework;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the TaskCommandLineEventArgs class.
    /// </summary>
    [TestFixture]
    public class TaskCommandLineEventArgs_Tests
    {
        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Test]
        public void EventArgsCtors()
        {
            TaskCommandLineEventArgs taskCommandLineEvent = new TaskCommandLineEventArgs2();
            taskCommandLineEvent = new TaskCommandLineEventArgs("Commandline", "taskName", MessageImportance.High);
            taskCommandLineEvent = new TaskCommandLineEventArgs("Commandline", "taskName", MessageImportance.High, DateTime.Now);
            taskCommandLineEvent = new TaskCommandLineEventArgs(null, null, MessageImportance.High);
            taskCommandLineEvent = new TaskCommandLineEventArgs(null, null, MessageImportance.High, DateTime.Now);
        }

        /// <summary>
        /// Create a derrived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class TaskCommandLineEventArgs2 : TaskCommandLineEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public TaskCommandLineEventArgs2() : base()
            {
            }
        }
    }
}