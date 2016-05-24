﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Sdl.Community.StarTransit.Shared.Models;
using Sdl.Community.StarTransit.Shared.Utils;
using Sdl.Core.Globalization;

namespace Sdl.Community.StarTransit.Shared.Services
{
    public class PackageService
    {
        private readonly List<KeyValuePair<string, string>> _dictionaryPropetries =
            new List<KeyValuePair<string, string>>();

        private Dictionary<string, List<KeyValuePair<string, string>>> _pluginDictionary =
            new Dictionary<string, List<KeyValuePair<string, string>>>();

        private PackageModel _package = new PackageModel();
        private const char LanguageTargetSeparator = '|';

        /// <summary>
        /// Opens a ppf package and saves to files to temp folder
        /// </summary>
        /// <param name="packagePath"></param>
        /// <param name="pathToTempFolder"></param>
        /// <returns>Task<PackageModel></returns>
        public async Task<PackageModel> OpenPackage(string packagePath, string pathToTempFolder)
        {

            var entryName = string.Empty;


            using (var archive = ZipFile.OpenRead(packagePath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    var subdirectoryPath = Path.GetDirectoryName(entry.FullName);
                    if (!Directory.Exists(Path.Combine(pathToTempFolder, subdirectoryPath)))
                    {
                        Directory.CreateDirectory(Path.Combine(pathToTempFolder, subdirectoryPath));
                    }
                    entry.ExtractToFile(Path.Combine(pathToTempFolder, entry.FullName));

                    if (entry.FullName.EndsWith(".PRJ", StringComparison.OrdinalIgnoreCase))
                    {
                        entryName = entry.FullName;
                    }

                }
            }

            return await ReadProjectMetadata(pathToTempFolder, entryName);
        }

        /// <summary>
        /// Reads the metadata from .PRJ file
        /// </summary>
        /// <param name="pathToTempFolder"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private async Task<PackageModel> ReadProjectMetadata(string pathToTempFolder, string fileName)
        {
            var filePath = Path.Combine(pathToTempFolder, fileName);
            var keyProperty = string.Empty;


            using (var reader = new StreamReader(filePath))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {


                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        var valuesDictionaries = new List<KeyValuePair<string, string>>();
                        if (keyProperty != string.Empty && _dictionaryPropetries.Count != 0)
                        {
                            valuesDictionaries.AddRange(
                                _dictionaryPropetries.Select(
                                    property => new KeyValuePair<string, string>(property.Key, property.Value)));
                            _pluginDictionary.Add(keyProperty, valuesDictionaries);
                            _dictionaryPropetries.Clear();
                        }

                        var firstPosition = line.IndexOf("[", StringComparison.Ordinal) + 1;
                        var lastPosition = line.IndexOf("]", StringComparison.Ordinal) - 1;
                        keyProperty = line.Substring(firstPosition, lastPosition);

                    }
                    else
                    {
                        var properties = line.Split('=');
                        _dictionaryPropetries.Add(new KeyValuePair<string, string>(properties[0], properties[1]));


                    }


                }
            }

            var packageModel = await CreateModel(pathToTempFolder);


            _package = packageModel;
            return packageModel;
        }

        /// <summary>
        /// Creates a package model
        /// </summary>
        /// <param name="pathToTempFolder"></param>
        /// <returns></returns>
        private async Task<PackageModel> CreateModel(string pathToTempFolder)
        {
            var model = new PackageModel();
            var languagePair = new LanguagePair();
            var sourceLanguageCode = 0;
            var targetLanguageCode = 0;

            var languagePairList = new List<LanguagePair>();
            if (_pluginDictionary.ContainsKey("Admin"))
            {
                var propertiesDictionary = _pluginDictionary["Admin"];
                foreach (var item in propertiesDictionary)
                {
                    if (item.Key == "ProjectName")
                    {
                        model.Name = item.Value;
                    }
                }
            }

            if (_pluginDictionary.ContainsKey("Languages"))
            {
                var propertiesDictionary = _pluginDictionary["Languages"];
                foreach (var item in propertiesDictionary)
                {
                    if (item.Key == "SourceLanguage")
                    {
                        sourceLanguageCode = int.Parse(item.Value);
                        languagePair.SourceLanguage = Language(sourceLanguageCode);

                    }
                    if (item.Key == "TargetLanguages")
                    {
                        //we assume languages code are separated by "|"
                        var languages = item.Value.Split(LanguageTargetSeparator);

                        foreach (var language in languages)
                        {
                            targetLanguageCode = int.Parse(language);
                            var cultureInfo = Language(targetLanguageCode);
                            var pair = new LanguagePair
                            {
                                SourceLanguage = languagePair.SourceLanguage,
                                TargetLanguage = cultureInfo
                            };
                            languagePairList.Add(pair);
                        }
                    }
                }
            }
            model.LanguagePairs = languagePairList;

            //for source
            var sourceFilesAndTmsPath = GetFilesAndTmsFromTempFolder(pathToTempFolder, sourceLanguageCode);
            var filesAndMetadata = ReturnSourceFilesNameAndMetadata(sourceFilesAndTmsPath);
            AddSourceFilesAndTmsToModel(model, filesAndMetadata, sourceLanguageCode);

           
            //for target
            var targetFilesAndTmsPath = GetFilesAndTmsFromTempFolder(pathToTempFolder, targetLanguageCode);
            AddTargetFilesAndTmsToModel(model, targetFilesAndTmsPath, targetLanguageCode);

            return model;

        }

        private void AddTargetFilesAndTmsToModel(PackageModel model, List<string> targetFilesAndTmsPath, int targetLanguageCode)
        {
            var targetLanguage = Language(targetLanguageCode);
            var pathToTargetFiles = new List<string>();

            foreach (var file in targetFilesAndTmsPath)
            {
                var guid = IsTmFile(file);
                foreach (var language in model.LanguagePairs)
                {
                    if (guid != Guid.Empty)
                    {
                        //selects the source tm which has the same id with the target tm id
                        var metaData =
                            (from pair in language.StarTranslationMemoryMetadatas where guid == pair.Id select pair)
                                .FirstOrDefault();
                        if (metaData != null)
                        {
                            metaData.TargetFile = file;
                        }

                    }
                    else
                    {
                        pathToTargetFiles.Add(file);
                    }
                    language.TargetFile = pathToTargetFiles;
                    language.TargetLanguage = targetLanguage;
                }

            }
        }

        private void AddSourceFilesAndTmsToModel(PackageModel model, Tuple<List<string>, List<StarTranslationMemoryMetadata>> sourceFilesAndTmsPath, int languageCode)
        {
            var sourceLanguage = Language(languageCode);
            var fileList = sourceFilesAndTmsPath.Item1;
            var tmMetadataList = sourceFilesAndTmsPath.Item2;
            bool hasTm;
            var languagePairList = new List<LanguagePair>();

            if (tmMetadataList.Count != 0)
            {
                hasTm = true;
            }
            else
            {
                hasTm = false;
            }

            var languagePair = new LanguagePair
            {
                HasTm = hasTm,
                SourceFile = fileList,
                StarTranslationMemoryMetadatas = tmMetadataList,
                SourceLanguage = sourceLanguage
            };
            languagePairList.Add(languagePair);
            model.LanguagePairs = languagePairList;
        }


        /// <summary>
        /// Check if is a tm
        /// </summary>
        /// <param name="file"></param>
        /// <returns>Tm id</returns>
        private Guid IsTmFile(string file)
        {
            var tmFile = XElement.Load(file);
            if (tmFile.Attribute("ExtFileType") != null)
            {

                var ffdNode =
                    (from ffd in tmFile.Descendants("FFD") select new Guid(ffd.Attribute("GUID").Value)).FirstOrDefault();
                return ffdNode;
            }

            return Guid.Empty;
        }

        private List<string> GetFilesAndTmsFromTempFolder(string pathToTempFolder, int languageCode)
        {
            var language = Language(languageCode);
            var extension = language.ThreeLetterWindowsLanguageName;
            var filesAndTms =
                Directory.GetFiles(pathToTempFolder, "*." + extension, SearchOption.AllDirectories).ToList();

            return filesAndTms;
        }

        private Tuple<List<string>,List<StarTranslationMemoryMetadata>> ReturnSourceFilesNameAndMetadata(List<string> filesAndTmsList )
        {
           
            var translationMemoryMetadataList = new List<StarTranslationMemoryMetadata>();
            var fileNames = new List<string>();

            foreach (var file in filesAndTmsList)
            {
                var guid = IsTmFile(file);
                if (guid != Guid.Empty)
                {
                    var metadata = new StarTranslationMemoryMetadata
                    {
                        Id = guid,
                        SourceFile = file
                    };
                    translationMemoryMetadataList.Add(metadata);

                }
                else
                {
                    fileNames.Add(file);
                }
            }

            return new Tuple<List<string>, List<StarTranslationMemoryMetadata>>(fileNames,translationMemoryMetadataList);
        }
        
   
        /// <summary>
        /// Helper method which to get language from language code
        /// </summary>
        /// <param name="languageCode"></param>
        /// <returns>CultureInfo of the language</returns>
        private CultureInfo Language(int languageCode)
        {
            return new CultureInfo(languageCode);
        }


    }
}