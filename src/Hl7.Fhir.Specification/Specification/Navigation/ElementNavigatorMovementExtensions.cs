﻿/* 
 * Copyright (c) 2016, Furore (info@furore.com) and contributors
 * See the file CONTRIBUTORS for details.
 * 
 * This file is licensed under the BSD 3-Clause license
 * available at https://raw.githubusercontent.com/ewoutkramer/fhir-net-api/master/LICENSE
 */

using System;
using System.Collections.Generic;
using System.Linq;
using Hl7.Fhir.Support;
using System.Diagnostics;
using Hl7.Fhir.Model;

namespace Hl7.Fhir.Specification.Navigation
{
    public static class NamedNavigationExtensions
    {
        /// <summary>Move the navigator to the first child element with the specified name, if it exists.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool MoveToChild(this ElementDefinitionNavigator nav, string name)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            if (nav.MoveToFirstChild())
            {
                do
                {
                    if(nav.PathName == name) return true;
                }
                while (nav.MoveToNext());
                nav.MoveToParent();
            }

            return false;
        }

        /// <summary>Move the navigator to the first following sibling element with the specified name, if it exists.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool MoveToNext(this ElementDefinitionNavigator nav, string name)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            var bm = nav.Bookmark();

            while (nav.MoveToNext())
            {
                if (nav.PathName == name) return true;
            }

            nav.ReturnToBookmark(bm);
            return false;           
        }

        // [WMR 20160802] NEW

        /// <summary>Move the navigator to the next type slice of the (choice) element with the specified name, if it exists.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool MoveToNextTypeSlice(this ElementDefinitionNavigator nav, string name)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            var bm = nav.Bookmark();

            while (nav.MoveToNext())
            {
                if (ElementDefinitionNavigator.IsRenamedChoiceElement(name, nav.PathName)) return true;
            }

            nav.ReturnToBookmark(bm);
            return false;
        }

        /// <summary>Determines if the element with the specified name represents a type slice for the current (choice) element.</summary>
        /// <returns><c>true</c> if the element name represents a type slice of the current element, <c>false</c> otherwise.</returns>
        internal static bool IsCandidateTypeSlice(this ElementDefinitionNavigator nav, string diffName)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            return ElementDefinitionNavigator.IsRenamedChoiceElement(nav.PathName, diffName);
        }

        /// <summary>Move to last direct child element with same path as current element.</summary>
        /// <param name="nav">An <see cref="ElementDefinitionNavigator"/> instance.</param>
        /// <param name="sliceName">The optional target slice name, or <c>null</c>. Used for reslicing.</param>
        /// <returns><c>true</c> if the cursor has moved at least a single element, <c>false</c> otherwise</returns>
        internal static bool MoveToLastSlice(this ElementDefinitionNavigator nav, string sliceName)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            if (nav.Current == null) { throw Error.Argument("nav", "Cannot move to last slice. Current node is not set."); }
            // if (nav.Current.Base == null) { throw Error.Argument("nav", "Cannot move to last slice. Current node has no Base.path component (path '{0}').".FormatWith(nav.Path)); }

            var bm = nav.Bookmark();
            var basePath = nav.Current.Base != null ? nav.Current.Base.Path : nav.Path;
            // if (string.IsNullOrEmpty(basePath)) { throw Error.Argument("nav", "Cannot move to last slice. Current node has no Base.path component (path '{0}').".FormatWith(nav.Path)); }

            var result = false;
            // while (nav.MoveToNext())
            do
            {
                var baseComp = nav.Current.Base != null ? nav.Current.Base.Path : nav.Path;
                if (baseComp != null && (baseComp == basePath || ElementDefinitionNavigator.IsRenamedChoiceElement(basePath, baseComp)))
                {
                    if (sliceName == null || nav.Current.Name == sliceName)
                    {
                        // Match, advance cursor
                        bm = nav.Bookmark();
                        result = true;
                    }
                    // Otherwise advance to next slice entry
                }
                else
                {
                    // Mismatch, back up to previous element and exit
                    nav.ReturnToBookmark(bm);
                    break;
                }
            } while (nav.MoveToNext());
            return result;
        }

        /// <summary>
        /// If the current element has the specified name, then maintain position and return true.
        /// Otherwise move to the next sibling element with the specified slice name, if it exists.
        /// </summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        internal static bool MoveToNextSlice(this ElementDefinitionNavigator nav, string sliceName)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            if (nav.Current == null) { throw Error.Argument("nav", "Cannot move to next slice. Current node is not set."); }

            var bm = nav.Bookmark();
            var basePath = nav.Current.Base != null ? nav.Current.Base.Path : nav.Path;

            var result = false;
            do
            {
                var baseComp = nav.Current.Base != null ? nav.Current.Base.Path : nav.Path;
                if (baseComp != null && (baseComp == basePath || ElementDefinitionNavigator.IsRenamedChoiceElement(basePath, baseComp)))
                {
                    if (nav.Current.Name == sliceName)
                    {
                        // Match!
                        result = true;
                        break;
                    }
                }
                else
                {
                    // Mismatch, back up to previous element and exit
                    nav.ReturnToBookmark(bm);
                    break;
                }
            } while (nav.MoveToNext());
            return result;
        }

        /// <summary>Move the navigator to the first preceding sibling element with the specified name, if it exists.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool MoveToPrevious(this ElementDefinitionNavigator nav, string name)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            var bm = nav.Bookmark();

            while (nav.MoveToPrevious())
            {
                if (nav.PathName == name) return true;
            }

            nav.ReturnToBookmark(bm);
            return false;
        }


        /// <summary>Move the navigator to the first preceding or following sibling element with the specified name, if it exists.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool MoveTo(this ElementDefinitionNavigator nav, string name)
        {
            // MoveNext method performs parameter validation
            return MoveToNext(nav, name) || MoveToPrevious(nav,name);
        }

        // [WMR 20160802] NEW - Move to the specified ElementDefinition

        /// <summary>Move the navigator to the specified element.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool MoveTo(this ElementDefinitionNavigator nav, ElementDefinition element)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            // Validated by Bookmark.FromElement
            // if (element == null) { throw Error.ArgumentNull(nameof(element)); }
            var bm = Bookmark.FromElement(element);
            return nav.ReturnToBookmark(bm);
        }

        /// <summary>Move the navigator to the first element with the specified path.</summary>
        /// <returns><c>true</c> if succesful, <c>false</c> otherwise.</returns>
        public static bool JumpToFirst(this ElementDefinitionNavigator nav, string path)
        {
            // Find method performs parameter validation
            // if (nav == null) { throw Error.ArgumentNull(nameof(nav)); }
            // if (path == null) { throw Error.ArgumentNull(nameof(path)); }

            var matches = Find(nav, path);

            if (matches.Any())
            {
                nav.ReturnToBookmark(matches.First());
                return true;
            }

            return false;
        }


        /// <summary>Find all existing elements with the specified path.</summary>
        /// <returns>A sequence of <see cref="Bookmark"/> values.</returns>
        public static IEnumerable<Bookmark> Find(this ElementDefinitionNavigator nav, string path)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            if (path == null) { throw Error.ArgumentNull("path"); }

            var parts = path.Split('.');

            var bm = nav.Bookmark();
            nav.Reset();
            var result = locateChildren(nav, parts, partial: false);
            nav.ReturnToBookmark(bm);

            return result;
        }


        public static IEnumerable<Bookmark> Approach(this ElementDefinitionNavigator nav, string path)
        {
            if (nav == null) { throw Error.ArgumentNull("nav"); }
            if (path == null) { throw Error.ArgumentNull("path"); }

            var parts = path.Split('.');

            var bm = nav.Bookmark();
            nav.Reset();
            var result = locateChildren(nav, parts, partial: true);
            nav.ReturnToBookmark(bm);

            return result;
        }

        private static IEnumerable<Bookmark> locateChildren(ElementDefinitionNavigator nav, IEnumerable<string> path, bool partial)
        {
            Debug.Assert(nav != null); // Caller should validate

            var child = path.First();
            var rest = path.Skip(1);

            var bm = nav.Bookmark();

            if (nav.MoveToChild(child))
            {
                var result = new List<Bookmark>();

                do
                {
                    if (!rest.Any())
                    {
                        // Exact match!
                        result.Add(nav.Bookmark());
                    }
                    else if (!nav.HasChildren && partial)
                    {
                        // This is as far as we can get in this structure,
                        // so this is a hit too if partial hits are OK
                        result.Add(nav.Bookmark());
                    }
                    else
                    {
                        // So, no hit, but we have children that might fit the bill.
                        result.AddRange(locateChildren(nav, rest, partial));
                    }

                    // Try this for the other matching siblings too...
                }
                while (nav.MoveToNext(child));

                // We've scanned all my children and collected the results,
                // move the navigator back to where we were before
                nav.ReturnToBookmark(bm);
                return result;
            }
            else
                return Enumerable.Empty<Bookmark>();
        }
    }
}
