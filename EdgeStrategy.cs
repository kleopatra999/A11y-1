﻿using Interop.UIAutomationCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Edge.A11y
{
    /// <summary>
    /// A strategy for testing Edge Accessibility as scored at 
    /// http://html5accessibility.com/
    /// </summary>
    internal class EdgeStrategy : TestStrategy
    {
        public EdgeStrategy(string repositoryPath = "https://cdn.rawgit.com/DHBrett/AT-browser-tests/gh-pages/test-files/"){
            _driverManager = new DriverManager(TimeSpan.FromSeconds(10));
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(2));//Wait for the browser to load before we start searching
            _RepositoryPath = repositoryPath;
        }

        /// <summary>
        /// This handles most of the work of the test cases.
        ///
        /// N.B. all the test case results are returned in pairs, since we need to be
        /// able to give half scores for certain results.
        /// </summary>
        /// <param name="testData">An object which stores information about the
        /// expected results</param>
        /// <param name="driverManager"></param>
        /// <returns></returns>
        internal override IEnumerable<TestCaseResult> TestElement(TestData testData)
        {
            //Find the browser
            var browserElement = EdgeA11yTools.FindBrowserDocument(0);
            if (browserElement == null)
            {
                return Fail(testData._TestName, "Unable to find the browser");
            }

            //Find elements using ControlType or the alternate search strategy
            HashSet<string> foundControlTypes;
            var testElements = EdgeA11yTools.SearchChildren(browserElement, testData._ControlType, testData._SearchStrategy, out foundControlTypes);
            if (testElements.Count == 0)
            {
                return Fail(testData._TestName, testData._SearchStrategy == null ? 
                    "Unable to find the element, found these instead: " + foundControlTypes.Aggregate((a, b) => a + ", " + b):
                    "Unable to find the element using the alternate search strategy");
            }

            string result = "";
            //This is used if the test passes but there is something to report
            string note = null;
            var elementConverter = new ElementConverter();

            //If necessary, check localized control type
            if (testData._LocalizedControlType != null)
            {
                foreach (var element in testElements)
                {
                    if (!element.CurrentLocalizedControlType.Equals(testData._LocalizedControlType, StringComparison.OrdinalIgnoreCase))
                    {
                        var error = "\nElement did not have the correct localized control type. Expected:" +
                            testData._LocalizedControlType + " Actual:" + element.CurrentLocalizedControlType;
                        if (!result.Contains(error))
                        {
                            result += error;
                        }
                    }
                }
            }

            //If necessary, check landmark and localized landmark types
            if (testData._LandmarkType != null)
            {
                foreach (var element in testElements)
                {
                    var five = element as IUIAutomationElement5;
                    var convertedLandmark = elementConverter.GetElementNameFromCode(five.CurrentLandmarkType);
                    var localizedLandmark = five.CurrentLocalizedLandmarkType;

                    if (convertedLandmark != testData._LandmarkType)
                    {
                        var error = "\nElement did not have the correct landmark type. Expected:" +
                            testData._LandmarkType + " Actual:" + convertedLandmark + "\n";
                        if (!result.Contains(error))
                        {
                            result += error;
                        }
                    }

                    if (localizedLandmark != testData._LocalizedLandmarkType)
                    {
                        var error = "\nElement did not have the correct localized landmark type. Expected:" +
                            testData._LocalizedLandmarkType + " Actual:" + localizedLandmark + "\n";
                        if (!result.Contains(error))
                        {
                            result += error;
                        }
                    }
                }
            }

            //If necessary, check keboard accessibility
            var tabbable = EdgeA11yTools.TabbableIds(_driverManager);
            if (testData._KeyboardElements != null && testData._KeyboardElements.Count > 0)
            {
                foreach (var e in testData._KeyboardElements)
                {
                    if (!tabbable.Contains(e))
                    {
                        result += "\nCould not access element with id: '" + e + "' by tab";
                    }
                }
            }


            try
            {
                //If necessary, check any additional requirements
                if (testData._AdditionalRequirement != null)
                {
                    testElements = EdgeA11yTools.SearchChildren(browserElement, testData._ControlType, testData._SearchStrategy, out foundControlTypes);
                    var additionalRequirementResult = testData._AdditionalRequirement(testElements, _driverManager, tabbable).Trim();
                    if (additionalRequirementResult != TestData.ARPASS)
                    {
                        result += "\n" + additionalRequirementResult;
                    }
                }
            }
            catch(Exception ex)
            {
                result += "\nCaught exception during test execution, ERROR: " + ex.Message + "\nCallStack:\n" + ex.StackTrace;
            }

            if (result != "")
            {
                return Half(testData._TestName, result.Trim());
            }

            return Pass(testData._TestName, note);
        }
    }
}
