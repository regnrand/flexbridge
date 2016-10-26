﻿// --------------------------------------------------------------------------------------------
// Copyright (C) 2010-2016 SIL International. All rights reserved.
//
// Distributable under the terms of the MIT License, as specified in the license.rtf file.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using LibFLExBridgeChorusPlugin.Contexts.Anthropology;
using LibFLExBridgeChorusPlugin.Contexts.General;
using LibFLExBridgeChorusPlugin.Contexts.Linguistics;
using LibFLExBridgeChorusPlugin.Contexts.Scripture;
using LibFLExBridgeChorusPlugin.Infrastructure;
using LibFLExBridgeChorusPlugin.DomainServices;
using SIL.Progress;

namespace LibFLExBridgeChorusPlugin.Contexts
{
	internal static class BaseDomainServices
	{
		internal static void PushHumptyOffTheWall(IProgress progress, bool writeVerbose, string pathRoot,
			IDictionary<string, XElement> wellUsedElements,
			Dictionary<string, SortedDictionary<string, byte[]>> classData,
			Dictionary<string, string> guidToClassMapping)
		{
			// NB: Don't even think of changing the order these methods are called in.
			LinguisticsDomainServices.WriteNestedDomainData(progress, writeVerbose, pathRoot, wellUsedElements, classData, guidToClassMapping);
			AnthropologyDomainServices.WriteNestedDomainData(progress, writeVerbose, pathRoot, wellUsedElements, classData, guidToClassMapping);
			if (MetadataCache.MdCache.ModelVersion < 9000000)
			{
				ScriptureDomainServices.WriteNestedDomainData(progress, writeVerbose, pathRoot, wellUsedElements, classData, guidToClassMapping);
			}
			GeneralDomainServices.WriteNestedDomainData(progress, writeVerbose, pathRoot, wellUsedElements, classData, guidToClassMapping);
		}

		internal static SortedDictionary<string, XElement> PutHumptyTogetherAgain(IProgress progress, bool writeVerbose, string pathRoot)
		{
			var retval = new SortedDictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);

			var sortedData = new SortedDictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			var highLevelData = new SortedDictionary<string, XElement>(StringComparer.OrdinalIgnoreCase);
			// NB: Don't even think of changing the order these methods are called in.
			GeneralDomainServices.FlattenDomain(progress, writeVerbose, highLevelData, sortedData, pathRoot);
			CmObjectFlatteningService.CombineData(retval, sortedData);
			if (MetadataCache.MdCache.ModelVersion < 9000000)
			{
				ScriptureDomainServices.FlattenDomain(progress, writeVerbose, highLevelData, sortedData, pathRoot);
			}
			CmObjectFlatteningService.CombineData(retval, sortedData);
			AnthropologyDomainServices.FlattenDomain(progress, writeVerbose, highLevelData, sortedData, pathRoot);
			CmObjectFlatteningService.CombineData(retval, sortedData);
			LinguisticsDomainServices.FlattenDomain(progress, writeVerbose, highLevelData, sortedData, pathRoot);
			CmObjectFlatteningService.CombineData(retval, sortedData);

			foreach (var highLevelElement in highLevelData.Values)
			{
				retval[highLevelElement.Attribute(FlexBridgeConstants.GuidStr).Value.ToLowerInvariant()] = highLevelElement;
			}

			return retval;
		}

		internal static void RemoveDomainData(string pathRoot)
		{
			LinguisticsDomainServices.RemoveBoundedContextData(pathRoot);
			AnthropologyDomainServices.RemoveBoundedContextData(pathRoot);
			if (MetadataCache.MdCache.ModelVersion < 9000000)
			{
				ScriptureDomainServices.RemoveBoundedContextData(pathRoot);
			}
			GeneralDomainServices.RemoveBoundedContextData(pathRoot);
		}

		internal static void RemoveBoundedContextDataCore(string contextBaseDir)
		{
			if (!Directory.Exists(contextBaseDir))
				return;

			foreach (var pathname in Directory.GetFiles(contextBaseDir, "*.*", SearchOption.AllDirectories)
				.Where(pathname => Path.GetExtension(pathname).ToLowerInvariant() != ".chorusnotes"))
			{
				File.Delete(pathname);
			}

			FileWriterService.RemoveEmptyFolders(contextBaseDir, true);
		}

		/************************ Basic operations above here. ****************/

		internal static void RestoreElement(
			string pathname,
			SortedDictionary<string, XElement> sortedData,
			XElement owningElement, string owningPropertyName,
			XElement ownedElement)
		{
			CmObjectFlatteningService.FlattenOwnedObject(
				pathname,
				sortedData,
				ownedElement,
				owningElement.Attribute(FlexBridgeConstants.GuidStr).Value, owningElement, owningPropertyName); // Restore 'ownerguid' to ownedElement.
		}

		internal static void RestoreObjsurElement(XElement owningPropertyElement, XElement ownedElement)
		{
			AddObjSurElement(owningPropertyElement, ownedElement);
		}

		internal static void RestoreObjsurElement(XContainer owningElement, string propertyName, XElement ownedElement)
		{
			AddObjSurElement(owningElement.Element(propertyName), ownedElement);
		}

		internal static void AddObjSurElement(XElement parentProperty, XElement ownedElement)
		{
			parentProperty.Add(CreateObjSurElement(ownedElement.Attribute(FlexBridgeConstants.GuidStr).Value));
		}

		internal static XElement CreateObjSurElement(string ownedGuid)
		{
			return CreateObjSurElement(ownedGuid, "o");
		}

		internal static XElement CreateObjSurElement(string ownedGuid, string typeValue)
		{
			return new XElement(FlexBridgeConstants.Objsur, CreateAttributes(ownedGuid, typeValue));
		}

		internal static IEnumerable<XAttribute> CreateAttributes(string ownedGuid, string typeValue)
		{
			return new List<XAttribute>
					{
						new XAttribute(FlexBridgeConstants.GuidStr, ownedGuid.ToLowerInvariant()),
						new XAttribute("t", typeValue)
					};
		}

		internal static void NestStylesPropertyElement(
			IDictionary<string, SortedDictionary<string, byte[]>> classData,
			Dictionary<string, string> guidToClassMapping,
			XElement stylesProperty,
			string outputPathname)
		{
			if (stylesProperty == null)
				return;
			var styleObjSurElements = stylesProperty.Elements().ToList();
			if (!styleObjSurElements.Any())
				return;

			// Use only one file for all of them.
			var root = new XElement(FlexBridgeConstants.Styles);
			foreach (var styleObjSurElement in styleObjSurElements)
			{
				var styleGuid = styleObjSurElement.Attribute(FlexBridgeConstants.GuidStr).Value.ToLowerInvariant();
				var className = guidToClassMapping[styleGuid];
				var style = LibFLExBridgeUtilities.CreateFromBytes(classData[className][styleGuid]);
				CmObjectNestingService.NestObject(false, style, classData, guidToClassMapping);
				root.Add(style);
			}

			FileWriterService.WriteNestedFile(outputPathname, root);

			stylesProperty.RemoveNodes();
		}

		internal static void ReplaceElementNameWithAndAddClassAttribute(string replacementElementName, XElement elementToRename)
		{
			var oldElementName = elementToRename.Name.LocalName;
			elementToRename.Name = replacementElementName;
			var sortedAttrs = new SortedDictionary<string, XAttribute>(StringComparer.OrdinalIgnoreCase);
			foreach (var attr in elementToRename.Attributes())
				sortedAttrs.Add(attr.Name.LocalName, attr);
			sortedAttrs.Add(FlexBridgeConstants.Class, new XAttribute(FlexBridgeConstants.Class, oldElementName));
			elementToRename.Attributes().Remove();
			foreach (var sortedAttr in sortedAttrs.Values)
				elementToRename.Add(sortedAttr);
		}

		internal static List<string> GetGuids(XContainer owningElement, string propertyName)
		{
			var propElement = owningElement.Element(propertyName);

			return (propElement == null) ? new List<string>() : (from osEl in propElement.Elements(FlexBridgeConstants.Objsur)
																 select osEl.Attribute(FlexBridgeConstants.GuidStr).Value.ToLowerInvariant()).ToList();
		}
	}
}