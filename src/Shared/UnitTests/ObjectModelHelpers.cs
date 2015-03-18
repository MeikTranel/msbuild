﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;


using NUnit.Framework;

using Microsoft.Build.Construction;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;

namespace Microsoft.Build.UnitTests
{
    /*
     * Class:   ObjectModelHelpers
     *
     * Utility methods for unit tests that work through the object model.
     *
     */
    public static class ObjectModelHelpers
    {
        private const string msbuildNamespace = "http://schemas.microsoft.com/developer/msbuild/2003";
        private const string msbuildDefaultToolsVersion = MSBuildConstants.CurrentToolsVersion;
        private const string msbuildAssemblyVersion = MSBuildConstants.CurrentAssemblyVersion;
        private const string currentVisualStudioVersion = MSBuildConstants.CurrentVisualStudioVersion;

        /// <summary>
        /// Return the the current Visual Studio version
        /// </summary>
        internal static string CurrentVisualStudioVersion
        {
            get
            {
                return currentVisualStudioVersion;
            }
        }

        /// <summary>
        /// Return the default tools version 
        /// </summary>
        internal static string MSBuildDefaultToolsVersion
        {
            get
            {
                return msbuildDefaultToolsVersion;
            }
        }

        /// <summary>
        /// Return the current assembly version 
        /// </summary>
        internal static string MSBuildAssemblyVersion
        {
            get
            {
                return msbuildAssemblyVersion;
            }
        }


        /// <summary>
        /// Helper method to tell us whether a particular metadata name is an MSBuild well-known metadata
        /// (e.g., "RelativeDir", "FullPath", etc.)
        /// </summary>
        private static Hashtable s_builtInMetadataNames = null;
        static private bool IsBuiltInItemMetadataName(string metadataName)
        {
            if (s_builtInMetadataNames == null)
            {
                s_builtInMetadataNames = new Hashtable();

                Microsoft.Build.Utilities.TaskItem dummyTaskItem = new Microsoft.Build.Utilities.TaskItem();
                foreach (string builtInMetadataName in dummyTaskItem.MetadataNames)
                {
                    s_builtInMetadataNames[builtInMetadataName] = String.Empty;
                }
            }

            return s_builtInMetadataNames.Contains(metadataName);
        }

        internal delegate void MethodUnderTest();

        /// <summary>
        /// Gets an item list from the project and assert that it contains
        /// exactly one item with the supplied name.
        /// </summary>
        static internal ProjectItem AssertSingleItem(Project p, string type, string itemInclude)
        {
            ProjectItem[] items = p.GetItems(type).ToArray();
            int count = 0;
            foreach (ProjectItem item in items)
            {
                Assert.AreEqual(itemInclude.ToUpperInvariant(), item.EvaluatedInclude.ToUpperInvariant());
                ++count;
            }

            Assert.AreEqual(1, count);

            return items[0];
        }

        /// <summary>
        /// Helper that asserts if an exception of type specified is 
        /// not thrown when calling specified method
        /// </summary>
        /// <example>
        /// AssertThrows(typeof(InvalidOperationException), delegate { object o = ((IEnumerator)enumerator).Current; });
        /// </example>
        internal static void AssertThrows(Type exception, MethodUnderTest method)
        {
            try
            {
                method();
            }
            catch (Exception ex)
            {
                if (ex.GetType() == exception)
                {
                    return;
                }
            }
            Assert.Fail("Didn't throw " + exception.ToString());
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        static internal void AssertItemsMatch(string expectedItemsString, ITaskItem[] actualItems)
        {
            AssertItemsMatch(expectedItemsString, actualItems, true);
        }

        /// <summary>
        /// Amazingly sophisticated :) helper function to determine if the set of ITaskItems returned from 
        /// a task match the expected set of ITaskItems.  It can also check that the ITaskItems have the expected
        /// metadata, and that the ITaskItems are returned in the correct order.
        /// 
        /// The "expectedItemsString" is a formatted way of easily specifying which items you expect to see.
        /// The format is:
        /// 
        ///         itemspec1 :   metadataname1=metadatavalue1 ; metadataname2=metadatavalue2 ; ...
        ///         itemspec2 :   metadataname3=metadatavalue3 ; metadataname4=metadatavalue4 ; ...
        ///         itemspec3 :   metadataname5=metadatavalue5 ; metadataname6=metadatavalue6 ; ...
        /// 
        /// (Each item needs to be on its own line.)
        /// 
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <param name="actualItems"></param>
        /// <param name="orderOfItemsShouldMatch"></param>
        static internal void AssertItemsMatch(string expectedItemsString, ITaskItem[] actualItems, bool orderOfItemsShouldMatch)
        {
            List<ITaskItem> expectedItems = ParseExpectedItemsString(expectedItemsString);

            // Form a string of expected item specs.  For logging purposes only.
            StringBuilder expectedItemSpecs = new StringBuilder();
            foreach (ITaskItem expectedItem in expectedItems)
            {
                if (expectedItemSpecs.Length > 0)
                {
                    expectedItemSpecs.Append("; ");
                }

                expectedItemSpecs.Append(expectedItem.ItemSpec);
            }

            // Form a string of expected item specs.  For logging purposes only.
            StringBuilder actualItemSpecs = new StringBuilder();
            foreach (ITaskItem actualItem in actualItems)
            {
                if (actualItemSpecs.Length > 0)
                {
                    actualItemSpecs.Append("; ");
                }

                actualItemSpecs.Append(actualItem.ItemSpec);
            }

            bool outOfOrder = false;

            // Loop through all the actual items.
            for (int actualItemIndex = 0; actualItemIndex < actualItems.Length; actualItemIndex++)
            {
                ITaskItem actualItem = actualItems[actualItemIndex];

                // Loop through all the expected items to find one with the same item spec.
                ITaskItem expectedItem = null;
                int expectedItemIndex;
                for (expectedItemIndex = 0; expectedItemIndex < expectedItems.Count; expectedItemIndex++)
                {
                    if (expectedItems[expectedItemIndex].ItemSpec == actualItem.ItemSpec)
                    {
                        expectedItem = expectedItems[expectedItemIndex];

                        // If the items are expected to be in the same order, then the expected item
                        // should always be found at index zero, because we remove items from the expected
                        // list as we find them.
                        if ((expectedItemIndex != 0) && (orderOfItemsShouldMatch))
                        {
                            outOfOrder = true;
                        }

                        break;
                    }
                }

                Assert.IsNotNull(expectedItem, String.Format("Item '{0}' was returned but not expected.", actualItem.ItemSpec));

                // Make sure all the metadata on the expected item matches the metadata on the actual item.
                // Don't check built-in metadata ... only check custom metadata.
                foreach (string metadataName in expectedItem.MetadataNames)
                {
                    // This check filters out any built-in item metadata, like "RelativeDir", etc.
                    if (!IsBuiltInItemMetadataName(metadataName))
                    {
                        string expectedMetadataValue = expectedItem.GetMetadata(metadataName);
                        string actualMetadataValue = actualItem.GetMetadata(metadataName);

                        Assert.IsTrue
                            (
                                actualMetadataValue.Length > 0 || expectedMetadataValue.Length == 0,
                                string.Format("Item '{0}' does not have expected metadata '{1}'.", actualItem.ItemSpec, metadataName)
                            );

                        Assert.IsTrue
                            (
                                actualMetadataValue.Length == 0 || expectedMetadataValue.Length > 0,
                                string.Format("Item '{0}' has unexpected metadata {1}={2}.", actualItem.ItemSpec, metadataName, actualMetadataValue)
                            );

                        Assert.AreEqual
                            (
                                expectedMetadataValue,
                                actualMetadataValue,
                                string.Format
                                    (
                                        "Item '{0}' has metadata {1}={2} instead of expected {1}={3}.",
                                        actualItem.ItemSpec,
                                        metadataName,
                                        actualMetadataValue,
                                        expectedMetadataValue
                                    )
                            );
                    }
                }
                expectedItems.RemoveAt(expectedItemIndex);
            }

            // Log an error for any leftover items in the expectedItems collection.
            foreach (ITaskItem expectedItem in expectedItems)
            {
                Assert.Fail(String.Format("Item '{0}' was expected but not returned.", expectedItem.ItemSpec));
            }

            if (outOfOrder)
            {
                Console.WriteLine("ERROR:  Items were returned in the incorrect order...");
                Console.WriteLine("Expected:  " + expectedItemSpecs);
                Console.WriteLine("Actual:    " + actualItemSpecs);
                Assert.Fail("Items were returned in the incorrect order.  See 'Standard Out' tab for more details.");
            }
        }

        /// <summary>
        /// Used to compare the contents of two arrays. 
        /// </summary>
        internal static void AssertArrayContentsMatch(object[] expected, object[] actual)
        {
            if (expected == null)
            {
                Assert.IsNull(actual, "Expected a null array");
            }
            else
            {
                Assert.IsNotNull(actual, "Result should be non-null.");
            }

            Assert.AreEqual(expected.Length, actual.Length, "Expected array length of <" + expected.Length + "> but was <" + actual.Length + ">.");

            // Now that we've verified they're both non-null and of the same length, compare each item in the array. 
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i], actual[i], "At index " + i + " expected " + expected[i].ToString() + " but was " + actual.ToString());
            }
        }

        /// <summary>
        /// Parses the crazy string passed into AssertItemsMatch and returns a list of ITaskItems.
        /// </summary>
        /// <param name="expectedItemsString"></param>
        /// <returns></returns>
        static private List<ITaskItem> ParseExpectedItemsString(string expectedItemsString)
        {
            List<ITaskItem> expectedItems = new List<ITaskItem>();

            // First, parse this massive string that we've been given, and create an ITaskItem[] out of it,
            // so we can more easily compare it against the actual items.
            string[] expectedItemsStringSplit = expectedItemsString.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string singleExpectedItemString in expectedItemsStringSplit)
            {
                string singleExpectedItemStringTrimmed = singleExpectedItemString.Trim();
                if (singleExpectedItemStringTrimmed.Length > 0)
                {
                    int indexOfColon = singleExpectedItemStringTrimmed.IndexOf(": ");
                    if (indexOfColon == -1)
                    {
                        expectedItems.Add(new Microsoft.Build.Utilities.TaskItem(singleExpectedItemStringTrimmed));
                    }
                    else
                    {
                        // We found a colon, which means there's metadata in there.

                        // The item spec is the part before the colon.
                        string itemSpec = singleExpectedItemStringTrimmed.Substring(0, indexOfColon).Trim();

                        // The metadata is the part after the colon.
                        string itemMetadataString = singleExpectedItemStringTrimmed.Substring(indexOfColon + 1);

                        ITaskItem expectedItem = new Microsoft.Build.Utilities.TaskItem(itemSpec);

                        string[] itemMetadataPieces = itemMetadataString.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
                        foreach (string itemMetadataPiece in itemMetadataPieces)
                        {
                            string itemMetadataPieceTrimmed = itemMetadataPiece.Trim();
                            if (itemMetadataPieceTrimmed.Length > 0)
                            {
                                int indexOfEquals = itemMetadataPieceTrimmed.IndexOf('=');
                                Assert.IsTrue(indexOfEquals != -1, String.Format("Could not find <equals> in item metadata definition '{0}'", itemMetadataPieceTrimmed));

                                string itemMetadataName = itemMetadataPieceTrimmed.Substring(0, indexOfEquals).Trim();
                                string itemMetadataValue = itemMetadataPieceTrimmed.Substring(indexOfEquals + 1).Trim();

                                expectedItem.SetMetadata(itemMetadataName, itemMetadataValue);
                            }
                        }

                        expectedItems.Add(expectedItem);
                    }
                }
            }

            return expectedItems;
        }

        /// <summary>
        /// Assert that a given file exists within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        internal static void AssertFileExistsInTempProjectDirectory(string fileRelativePath)
        {
            AssertFileExistsInTempProjectDirectory(fileRelativePath, null);
        }

        /// <summary>
        /// Assert that a given file exists within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="message">Can be null.</param>
        internal static void AssertFileExistsInTempProjectDirectory(string fileRelativePath, string message)
        {
            if (message == null)
            {
                message = fileRelativePath + " doesn't exist, but it should.";
            }

            AssertFileExistenceInTempProjectDirectory(fileRelativePath, message, true);
        }

        /// <summary>
        /// Assert that a given file exists (or not) within the temp project directory.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="message">Can be null.</param>
        private static void AssertFileExistenceInTempProjectDirectory(string fileRelativePath, string message, bool exists)
        {
            Assert.AreEqual(exists, File.Exists(Path.Combine(TempProjectDir, fileRelativePath)), message);
        }

        /// <summary>
        /// Does certain replacements in a string representing the project file contents.
        /// This makes it easier to write unit tests because the author doesn't have
        /// to worry about escaping double-quotes, etc.
        /// </summary>
        /// <param name="projectFileContents"></param>
        /// <returns></returns>
        static internal string CleanupFileContents(string projectFileContents)
        {
            // Replace reverse-single-quotes with double-quotes.
            projectFileContents = projectFileContents.Replace("`", "\"");

            // Place the correct MSBuild namespace into the <Project> tag.
            projectFileContents = projectFileContents.Replace("msbuildnamespace", msbuildNamespace);
            projectFileContents = projectFileContents.Replace("msbuilddefaulttoolsversion", msbuildDefaultToolsVersion);
            projectFileContents = projectFileContents.Replace("msbuildassemblyversion", msbuildAssemblyVersion);

            return projectFileContents;
        }

        /// <summary>
        /// Normalizes all the whitespace in an Xml document so that two documents that
        /// differ only in whitespace can be easily compared to each other for sameness.
        /// </summary>
        /// <param name="xmldoc"></param>
        /// <returns></returns>
        static internal string NormalizeXmlWhitespace(XmlDocument xmldoc)
        {
            // Normalize all the whitespace by writing the Xml document out to a 
            // string, with PreserveWhitespace=false.
            xmldoc.PreserveWhitespace = false;
            StringWriter stringWriter = new StringWriter();
            xmldoc.Save(stringWriter);
            return stringWriter.ToString();
        }

        /// <summary>
        /// Create an MSBuild project file on disk and return the full path to it.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        static internal string CreateTempFileOnDisk(string fileContents, params object[] args)
        {
            return CreateTempFileOnDiskNoFormat(String.Format(fileContents, args));
        }

        /// <summary>
        /// Create an MSBuild project file on disk and return the full path to it.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        static internal string CreateTempFileOnDiskNoFormat(string fileContents)
        {
            string projectFilePath = FileUtilities.GetTemporaryFile();

            File.WriteAllText(projectFilePath, CleanupFileContents(fileContents));

            return projectFilePath;
        }

        /// <summary>
        /// Create a project in memory. Load up the given XML.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        static internal Project CreateInMemoryProject(string xml)
        {
            return CreateInMemoryProject(xml, new ConsoleLogger());
        }

        /// <summary>
        /// Create a project in memory. Load up the given XML.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        static internal Project CreateInMemoryProject(string xml, ILogger logger /* May be null */)
        {
            return CreateInMemoryProject(new ProjectCollection(), xml, logger);
        }

        /// <summary>
        /// Create an in-memory project and attach it to the passed-in engine.
        /// </summary>
        /// <param name="engine"></param>
        /// <param name="xml"></param>
        /// <param name="logger">May be null</param>
        /// <returns></returns>
        static internal Project CreateInMemoryProject(ProjectCollection e, string xml, ILogger logger /* May be null */)
        {
            return CreateInMemoryProject(e, xml, logger, null);
        }

        /// <summary>
        /// Create an in-memory project and attach it to the passed-in engine.
        /// </summary>
        /// <param name="logger">May be null</param>
        /// <param name="toolsVersion">May be null</param>
        static internal Project CreateInMemoryProject
            (
            ProjectCollection projectCollection,
            string xml,
            ILogger logger /* May be null */,
            string toolsVersion /* may be null */
            )
        {
            // Anonymous in-memory projects use the current directory for $(MSBuildProjectDirectory).
            // We need to set the directory to something reasonable.
            string originalDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));

            XmlReaderSettings readerSettings = new XmlReaderSettings();
            readerSettings.DtdProcessing = DtdProcessing.Ignore;

            Project project = new Project
                (
                XmlReader.Create(new StringReader(CleanupFileContents(xml)), readerSettings),
                null,
                toolsVersion,
                projectCollection
                );

            Guid guid = Guid.NewGuid();
            project.FullPath = Path.Combine(Path.GetTempPath(), "Temporary" + guid.ToString("N") + ".csproj");
            project.ReevaluateIfNecessary();

            if (logger != null)
            {
                project.ProjectCollection.RegisterLogger(logger);
            }

            // Return to the original directory.
            Directory.SetCurrentDirectory(originalDir);

            return project;
        }
        /// <summary>
        /// Creates a project in memory and builds the default targets.  The build is
        /// expected to succeed.
        /// </summary>
        /// <param name="projectContents"></param>
        /// <returns></returns>
        internal static MockLogger BuildProjectExpectSuccess
            (
            string projectContents
            )
        {
            MockLogger logger = new MockLogger();
            BuildProjectExpectSuccess(projectContents, logger);
            return logger;
        }

        internal static void BuildProjectExpectSuccess
           (
           string projectContents,
            ILogger logger
           )
        {
            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents, logger);

            bool success = project.Build(logger);
            Assert.IsTrue(success, "Build failed.  See Standard Out tab for details");
        }

        /// <summary>
        /// Creates a project in memory and builds the default targets.  The build is
        /// expected to fail.
        /// </summary>
        /// <param name="projectContents"></param>
        /// <returns></returns>
        internal static MockLogger BuildProjectExpectFailure
            (
            string projectContents
            )
        {
            MockLogger logger = new MockLogger();
            BuildProjectExpectFailure(projectContents, logger);

            return logger;
        }

        internal static void BuildProjectExpectFailure
            (
            string projectContents,
            ILogger logger
           )
        {
            Project project = ObjectModelHelpers.CreateInMemoryProject(projectContents, logger);

            bool success = project.Build(logger);
            Assert.IsFalse(success, "Build succeeded, but shouldn't have.  See Standard Out tab for details");
        }

        /// <summary>
        /// This helper method compares the final project contents with the expected
        /// value.
        /// </summary>
        /// <param name="project"></param>
        /// <param name="newExpectedProjectContents"></param>
        internal static void CompareProjectContents
            (
            Project project,
            string newExpectedProjectContents
            )
        {
            // Get the new XML for the project, normalizing the whitespace.
            string newActualProjectContents = project.Xml.RawXml;

            // Replace single-quotes with double-quotes, and normalize whitespace.
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(ObjectModelHelpers.CleanupFileContents(newExpectedProjectContents));
            newExpectedProjectContents = ObjectModelHelpers.NormalizeXmlWhitespace(xmldoc);

            // Compare the actual XML with the expected XML.
            Console.WriteLine("================================= EXPECTED ===========================================");
            Console.WriteLine(newExpectedProjectContents);
            Console.WriteLine();
            Console.WriteLine("================================== ACTUAL ============================================");
            Console.WriteLine(newActualProjectContents);
            Console.WriteLine();
            Assert.AreEqual(newExpectedProjectContents, newActualProjectContents, "Project XML does not match expected XML.  See 'Standard Out' tab for details.");
        }


        private static string s_tempProjectDir = null;

        /// <summary>
        /// Returns the path %TEMP%\TempDirForMSBuildUnitTests
        /// </summary>
        internal static string TempProjectDir
        {
            get
            {
                if (s_tempProjectDir == null)
                {
                    s_tempProjectDir = Path.Combine(Path.GetTempPath(), "TempDirForMSBuildUnitTests");
                }

                return s_tempProjectDir;
            }
        }

        /// <summary>
        /// Deletes the directory %TEMP%\TempDirForMSBuildUnitTests, and all its contents.
        /// </summary>
        internal static void DeleteTempProjectDirectory()
        {
            DeleteDirectory(TempProjectDir);
        }

        /// <summary>
        /// Deletes the directory and all its contents.
        /// </summary>
        internal static void DeleteDirectory(string dir)
        {
            // Manually deleting all children, but intentionally leaving the 
            // Temp project directory behind due to locking issues which were causing
            // failures in main on Amd64-WOW runs.

            // retries to deal with occasional locking issues where the file / directory can't be deleted to initially
            for (int retries = 0; retries < 5; retries++)
            {
                try
                {
                    if (Directory.Exists(dir))
                    {
                        foreach (string directory in Directory.GetDirectories(dir))
                        {
                            Directory.Delete(directory, true);
                        }

                        foreach (string file in Directory.GetFiles(dir))
                        {
                            File.Delete(file);
                        }
                    }

                    break;
                }
                catch (Exception ex)
                {
                    if (retries < 4)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    else
                    {
                        // All the retries have failed. We will now fail with the 
                        // actual problem now instead of with some more difficult-to-understand 
                        // issue later. 
                        throw ex;
                    }
                }
            }
        }

        /// <summary>
        /// Creates a file in the %TEMP%\TempDirForMSBuildUnitTests directory, after cleaning
        /// up the file contents (replacing single-back-quote with double-quote, etc.). 
        /// Silently OVERWRITES existing file.
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <param name="fileContents"></param>
        internal static string CreateFileInTempProjectDirectory(string fileRelativePath, string fileContents)
        {
            Assert.IsFalse(String.IsNullOrEmpty(fileRelativePath));
            string fullFilePath = Path.Combine(TempProjectDir, fileRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullFilePath));

            // retries to deal with occasional locking issues where the file can't be written to initially
            for (int retries = 0; retries < 5; retries++)
            {
                try
                {
                    File.WriteAllText(fullFilePath, CleanupFileContents(fileContents));
                    break;
                }
                catch (Exception ex)
                {
                    if (retries < 4)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    else
                    {
                        // All the retries have failed. We will now fail with the 
                        // actual problem now instead of with some more difficult-to-understand 
                        // issue later. 
                        throw ex;
                    }
                }
            }

            return fullFilePath;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        /// <param name="projectFileRelativePath"></param>
        /// <returns></returns>
        internal static MockLogger BuildTempProjectFileExpectSuccess(string projectFileRelativePath)
        {
            return BuildTempProjectFileWithTargetsExpectSuccess(projectFileRelativePath, null, null);
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        internal static MockLogger BuildTempProjectFileWithTargetsExpectSuccess(string projectFileRelativePath, string[] targets, IDictionary<string, string> additionalProperties)
        {
            MockLogger logger = new MockLogger();
            bool success = BuildTempProjectFileWithTargets(projectFileRelativePath, targets, additionalProperties, logger);

            Assert.IsTrue(success, "Build failed.  See Standard Out tab for details");

            return logger;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build succeeds.
        /// </summary>
        internal static MockLogger BuildTempProjectFileExpectFailure(string projectFileRelativePath)
        {
            MockLogger logger = new MockLogger();
            bool success = BuildTempProjectFileWithTargets(projectFileRelativePath, null, null, logger);

            Assert.IsFalse(success, "Build unexpectedly succeeded.  See Standard Out tab for details");

            return logger;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build succeeds.
        /// </summary>
        internal static MockLogger BuildTempProjectFileWithTargetsExpectFailure(string projectFileRelativePath, string[] targets, IDictionary<string, string> additionalProperties)
        {
            MockLogger logger = new MockLogger();
            bool success = BuildTempProjectFileWithTargets(projectFileRelativePath, targets, additionalProperties, logger);

            Assert.IsFalse(success, "Build unexpectedly succeeded.  See Standard Out tab for details");

            return logger;
        }

        /// <summary>
        /// Loads a project file from disk
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <returns></returns>
        internal static Project LoadProjectFileInTempProjectDirectory(string projectFileRelativePath)
        {
            return LoadProjectFileInTempProjectDirectory(projectFileRelativePath, false /* don't touch project*/);
        }

        /// <summary>
        /// Loads a project file from disk
        /// </summary>
        /// <param name="fileRelativePath"></param>
        /// <returns></returns>
        internal static Project LoadProjectFileInTempProjectDirectory(string projectFileRelativePath, bool touchProject)
        {
            string projectFileFullPath = Path.Combine(ObjectModelHelpers.TempProjectDir, projectFileRelativePath);

            ProjectCollection projectCollection = new ProjectCollection();

            Project project = new Project(projectFileFullPath, null, null, projectCollection);

            if (touchProject)
            {
                File.SetLastWriteTime(projectFileFullPath, DateTime.Now);
            }

            return project;
        }

        /// <summary>
        /// Builds a project file from disk, and asserts if the build does not succeed.
        /// </summary>
        /// <param name="projectFileRelativePath"></param>
        /// <param name="targets"></param>
        /// <param name="additionalProperties">Can be null.</param>
        /// <param name="logger"></param>
        /// <returns></returns>
        internal static bool BuildTempProjectFileWithTargets
        (
            string projectFileRelativePath,
            string[] targets,
            IDictionary<string, string> globalProperties,
            ILogger logger
        )
        {
            // Build the default targets.
            List<ILogger> loggers = new List<ILogger>(1);
            loggers.Add(logger);

            if (String.Equals(Path.GetExtension(projectFileRelativePath), ".sln"))
            {
                string projectFileFullPath = Path.Combine(ObjectModelHelpers.TempProjectDir, projectFileRelativePath);
                BuildRequestData data = new BuildRequestData(projectFileFullPath, globalProperties ?? new Dictionary<string, string>(), null, targets, null);
                BuildParameters parameters = new BuildParameters();
                parameters.Loggers = loggers;
                BuildResult result = BuildManager.DefaultBuildManager.Build(parameters, data);
                return result.OverallResult == BuildResultCode.Success;
            }
            else
            {
                Project project = LoadProjectFileInTempProjectDirectory(projectFileRelativePath);

                if (globalProperties != null)
                {
                    // add extra properties
                    foreach (KeyValuePair<string, string> globalProperty in globalProperties)
                    {
                        project.SetGlobalProperty(globalProperty.Key, globalProperty.Value);
                    }
                }

                return project.Build(targets, loggers);
            }
        }

        /// <summary>
        /// Delete any files in the list that currently exist.
        /// </summary>
        /// <param name="files"></param>
        internal static void DeleteTempFiles(string[] files)
        {
            for (int i = 0; i < files.Length; i++)
            {
                if (File.Exists(files[i])) File.Delete(files[i]);
            }
        }

        /// <summary>
        /// Returns the requested number of temporary files.
        /// </summary>
        internal static string[] GetTempFiles(int number)
        {
            return GetTempFiles(number, DateTime.Now);
        }

        /// <summary>
        /// Returns the requested number of temporary files, with the requested write time.
        /// </summary>
        internal static string[] GetTempFiles(int number, DateTime lastWriteTime)
        {
            string[] files = new string[number];

            for (int i = 0; i < number; i++)
            {
                files[i] = FileUtilities.GetTemporaryFile();
                File.SetLastWriteTime(files[i], lastWriteTime);
            }
            return files;
        }
    }

    /// <summary>
    /// Various generic unit test helper methods
    /// </summary>
    internal static partial class Helpers
    {
        /// <summary>
        /// Returns the count of objects returned by an enumerator
        /// </summary>
        internal static int Count(IEnumerable enumerable)
        {
            int i = 0;
            foreach (object o in enumerable)
            {
                i++;
            }

            return i;
        }

        /// <summary>
        /// Makes a temporary list out of an enumerable
        /// </summary>
        internal static List<T> MakeList<T>(IEnumerable<T> enumerable)
        {
            List<T> list = new List<T>();
            foreach (T item in enumerable)
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// Gets the first element in the enumeration, or null if there are none
        /// </summary>
        internal static T GetFirst<T>(IEnumerable<T> enumerable)
            where T : class
        {
            T first = null;

            foreach (T element in enumerable)
            {
                first = element;
                break;
            }

            return first;
        }

        /// <summary>
        /// Gets the last element in the enumeration, or null if there are none
        /// </summary>
        internal static T GetLast<T>(IEnumerable<T> enumerable)
            where T : class
        {
            T last = null;

            foreach (T item in enumerable)
            {
                last = item;
            }

            return last;
        }

        /// <summary>
        /// Makes a temporary dictionary out of an enumerable of keyvaluepairs.
        /// Throws 
        /// </summary>
        internal static Dictionary<string, V> MakeDictionary<V>(IEnumerable<KeyValuePair<string, V>> enumerable)
        {
            Dictionary<string, V> dictionary = new Dictionary<string, V>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, V> item in enumerable)
            {
                dictionary.Add(item.Key, item.Value);
            }
            return dictionary;
        }

        /// <summary>
        /// Verify that the two lists are value identical
        /// </summary>
        internal static void AssertListsValueEqual<T>(IList<T> one, IList<T> two)
        {
            Assert.AreEqual(one.Count, two.Count);

            for (int i = 0; i < one.Count; i++)
            {
                Assert.AreEqual(one[i], two[i]);
            }
        }

        /// <summary>
        /// Verify that the two collections are value identical
        /// </summary>
        internal static void AssertCollectionsValueEqual<T>(ICollection<T> one, ICollection<T> two)
        {
            Assert.AreEqual(one.Count, two.Count);

            foreach (T item in one)
            {
                Assert.IsTrue(two.Contains(item));
            }

            foreach (T item in two)
            {
                Assert.IsTrue(one.Contains(item));
            }
        }

        /// <summary>
        /// Verify that the two enumerables are value identical
        /// </summary>
        internal static void AssertEnumerationsValueEqual<T>(IEnumerable<T> one, IEnumerable<T> two)
        {
            List<T> listOne = new List<T>();
            List<T> listTwo = new List<T>();

            foreach (T item in one)
            {
                listOne.Add(item);
            }

            foreach (T item in two)
            {
                listTwo.Add(item);
            }

            AssertCollectionsValueEqual<T>(listOne, listTwo);
        }

        /// <summary>
        /// Build a project with the provided content in memory.
        /// Assert that it succeeded, and return the mock logger with the output.
        /// </summary>
        internal static MockLogger BuildProjectWithNewOMExpectSuccess(string content)
        {
            MockLogger logger;
            bool result;
            BuildProjectWithNewOM(content, out logger, out result, false);
            Assert.IsTrue(result);

            return logger;
        }

        /// <summary>
        /// Build a project in memory using the new OM
        /// </summary>
        private static void BuildProjectWithNewOM(string content, out MockLogger logger, out bool result, bool allowTaskCrash)
        {
            // Replace the crazy quotes with real ones
            content = ObjectModelHelpers.CleanupFileContents(content);

            Project project = new Project(XmlReader.Create(new StringReader(content)));
            logger = new MockLogger();
            logger.AllowTaskCrashes = allowTaskCrash;
            List<ILogger> loggers = new List<ILogger>();
            loggers.Add(logger);
            result = project.Build(loggers);
        }

        /// <summary>
        /// Build a project with the provided content in memory.
        /// Assert that it fails, and return the mock logger with the output.
        /// </summary>
        internal static MockLogger BuildProjectWithNewOMExpectFailure(string content, bool allowTaskCrash)
        {
            MockLogger logger;
            bool result;
            BuildProjectWithNewOM(content, out logger, out result, allowTaskCrash);
            Assert.IsFalse(result);
            return logger;
        }

        /// <summary>
        /// Compare the expected project XML to actual project XML, after doing a little normalization
        /// of 
        /// </summary>
        /// <param name="newExpectedProjectContents"></param>
        /// <param name="newActualProjectContents"></param>
        internal static void CompareProjectXml(string newExpectedProjectContents, string newActualProjectContents)
        {
            // Replace single-quotes with double-quotes, and normalize whitespace.
            XmlDocument xmldoc = new XmlDocument();
            xmldoc.LoadXml(ObjectModelHelpers.CleanupFileContents(newExpectedProjectContents));
            newExpectedProjectContents = ObjectModelHelpers.NormalizeXmlWhitespace(xmldoc);

            // Compare the actual XML with the expected XML.
            if (newExpectedProjectContents != newActualProjectContents)
            {
                Console.WriteLine("================================= EXPECTED ===========================================");
                Console.WriteLine(newExpectedProjectContents);
                Console.WriteLine();
                Console.WriteLine("================================== ACTUAL ============================================");
                Console.WriteLine(newActualProjectContents);
                Console.WriteLine();
                Assert.AreEqual(newExpectedProjectContents, newActualProjectContents, "Project XML does not match expected XML.  See 'Standard Out' tab for details.");
            }
        }

        /// <summary>
        /// Verify that the saved project content matches the provided content
        /// </summary>
        internal static void VerifyAssertProjectContent(string expected, Project project)
        {
            VerifyAssertProjectContent(expected, project.Xml);
        }

        /// <summary>
        /// Verify that the saved project content matches the provided content
        /// </summary>
        internal static void VerifyAssertProjectContent(string expected, ProjectRootElement project)
        {
            VerifyAssertLineByLine(expected, project.RawXml, true /* ignoreFirstLineOfActual */);
        }

        /// <summary>
        /// Verify that the expected content matches the actual content
        /// </summary>
        internal static void VerifyAssertLineByLine(string expected, string actual)
        {
            VerifyAssertLineByLine(expected, actual, false /* do not ignore first line */);
        }

        /// <summary>
        /// Creates a bunch of temporary files with the specified names and returns
        /// their full paths (so they can ultimately be cleaned up)
        /// </summary>
        internal static string[] CreateFiles(params string[] files)
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(directory);

            string[] result = new string[files.Length];

            for (int i = 0; i < files.Length; i++)
            {
                string fullPath = Path.Combine(directory, files[i]);
                File.WriteAllText(fullPath, String.Empty);
                result[i] = fullPath;
            }

            return result;
        }

        /// <summary>
        /// Deletes a bunch of files, including their containing directories
        /// if they become empty
        /// </summary>
        internal static void DeleteFiles(params string[] paths)
        {
            foreach (string path in paths)
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                string directory = Path.GetDirectoryName(path);
                if (Directory.Exists(directory) && (Directory.GetFileSystemEntries(directory).Length == 0))
                {
                    Directory.Delete(directory);
                }
            }
        }

        /// <summary>
        /// Given two methods accepting no parameters and returning none, verifies they
        /// both throw, and throw the same exception type.
        /// </summary>
        internal static void VerifyAssertThrowsSameWay(Action method1, Action method2)
        {
            Exception ex1 = null;
            Exception ex2 = null;

            try
            {
                method1();
            }
            catch (Exception ex)
            {
                ex1 = ex;
            }

            try
            {
                method2();
            }
            catch (Exception ex)
            {
                ex2 = ex;
            }

            if (ex1 == null && ex2 == null)
            {
                Assert.Fail("Neither threw");
            }

            Assert.AreNotEqual(null, ex1, "First method did not throw, second: {0}", ex2 == null ? "" : ex2.GetType() + ex2.Message);
            Assert.AreNotEqual(null, ex2, "Second method did not throw, first: {0}", ex1 == null ? "" : ex1.GetType() + ex1.Message);
            Assert.AreEqual(ex1.GetType(), ex2.GetType(), "Both methods threw but the first threw {0} '{1}' and the second threw {2} '{3}'", ex1.GetType(), ex1.Message, ex2.GetType(), ex2.Message);

            Console.WriteLine("COMPARE EXCEPTIONS:\n\n#1: {0}\n\n#2: {1}", ex1.Message, ex2.Message);
        }

        /// <summary>
        /// Given a method accepting no parameters and returning none, verifies it 
        /// throws an exception of the specified type.
        /// </summary>
        internal static void VerifyAssertThrows(Action method, Type expectedExceptionType)
        {
            try
            {
                method();
            }
            catch (Exception ex)
            {
                Assert.That(ex, Is.InstanceOf(expectedExceptionType));
                Console.WriteLine("Caught '{0}'", ex.Message);
                return;
            }

            Debugger.Break();
            Assert.Fail("Did not throw but expected {0} exception", expectedExceptionType.ToString());
        }

        /// <summary>
        /// Verify method throws invalid operation exception.
        /// </summary>
        internal static void VerifyAssertThrowsInvalidOperation(Action method)
        {
            VerifyAssertThrows(method, typeof(InvalidOperationException));
        }

        /// <summary>
        /// Verify that the expected content matches the actual content
        /// </summary>
        private static void VerifyAssertLineByLine(string expected, string actual, bool ignoreFirstLineOfActual)
        {
            string[] actualLines = SplitIntoLines(actual);

            if (ignoreFirstLineOfActual)
            {
                // Remove the first line of the actual content we got back,
                // since it's just the xml declaration, which we don't care about
                string[] temporary = new string[actualLines.Length - 1];

                for (int i = 0; i < temporary.Length; i++)
                {
                    temporary[i] = actualLines[i + 1];
                }

                actualLines = temporary;
            }

            string[] expectedLines = SplitIntoLines(expected);

            bool expectedAndActualDontMatch = false;
            for (int i = 0; i < Math.Min(actualLines.Length, expectedLines.Length); i++)
            {
                if (expectedLines[i] != actualLines[i])
                {
                    expectedAndActualDontMatch = true;
                    Console.WriteLine("<   " + expectedLines[i] + "\n>   " + actualLines[i] + "\n");
                }
            }

            if (actualLines.Length == expectedLines.Length && expectedAndActualDontMatch)
            {
                string output = "\r\n#################################Expected#################################\n" + String.Join("\r\n", expectedLines);
                output += "\r\n#################################Actual#################################\n" + String.Join("\r\n", actualLines);

                Assert.Fail(output);
            }

            if (actualLines.Length > expectedLines.Length)
            {
                Console.WriteLine("\n#################################Expected#################################\n" + String.Join("\n", expectedLines));
                Console.WriteLine("#################################Actual#################################\n" + String.Join("\n", actualLines));

                Assert.Fail("Expected content was shorter, actual had this extra line: '" + actualLines[expectedLines.Length] + "'");
            }
            else if (actualLines.Length < expectedLines.Length)
            {
                Console.WriteLine("\n#################################Expected#################################\n" + String.Join("\n", expectedLines));
                Console.WriteLine("#################################Actual#################################\n" + String.Join("\n", actualLines));

                Assert.Fail("Actual content was shorter, expected had this extra line: '" + expectedLines[actualLines.Length] + "'");
            }
        }

        /// <summary>
        /// Clear the dirty flag of a ProjectRootElement by saving to a dummy writer.
        /// </summary>
        internal static void ClearDirtyFlag(ProjectRootElement project)
        {
            project.Save(new StringWriter());
            Assert.IsFalse(project.HasUnsavedChanges);
        }

        /// <summary>
        /// Command you can pass to Exec to sleep for rough number of milliseconds.
        /// @for /l %i in (1,1,X) do "@dir %windir% > nul"
        /// sleeps for X/100 seconds, roughly
        /// This works around not having sleep.exe on the path.
        /// </summary>
        internal static string SleepCommandInMilliseconds(int milliseconds)
        {
            return String.Format(@"@for /l %25%25i in (1,1,{0}) do @dir %25windir%25 > nul", milliseconds / 10);
        }

        /// <summary>
        /// Command you can pass to Exec to sleep for rough number of seconds.
        /// @for /l %i in (1,1,X) do "@dir %windir% > nul"
        /// sleeps for X/100 seconds, roughly
        /// This works around not having sleep.exe on the path.
        /// </summary>
        /// <param name="seconds"></param>
        /// <returns></returns>
        internal static string SleepCommand(int seconds)
        {
            return SleepCommandInMilliseconds(seconds * 1000);
        }

        /// <summary>
        /// Break the provided string into an array, on newlines
        /// </summary>
        private static string[] SplitIntoLines(string content)
        {
            string[] result = content.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            return result;
        }
    }
}