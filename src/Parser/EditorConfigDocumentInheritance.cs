﻿using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Utilities;
using System;
using System.ComponentModel.Composition;
using System.IO;

namespace EditorConfig
{
    public partial class EditorConfigDocument
    {
        private IContentType _contentType;
        private EditorConfigDocument _parent;

        [Import]
        private ITextDocumentFactoryService DocumentService { get; set; }

        public string FileName { get; private set; }

        public EditorConfigDocument Parent
        {
            get
            {
                if (_parent == null)
                    _parent = InheritsFrom();

                return _parent;
            }
        }

        private void InitializeInheritance()
        {
            VsHelpers.SatisfyImportsOnce(this);

            var componentModel = (IComponentModel)Package.GetGlobalService(typeof(SComponentModel));
            var contentTypeRegistry = componentModel.DefaultExportProvider.GetExportedValue<IContentTypeRegistryService>();
            _contentType = contentTypeRegistry.GetContentType(Constants.LanguageName);

            if (DocumentService.TryGetTextDocument(TextBuffer, out ITextDocument doc))
            {
                FileName = doc.FilePath;
            }
        }

        private EditorConfigDocument InheritsFrom()
        {
            if (Root != null && Root.IsValid && Root.Value.Text.Equals("true", StringComparison.OrdinalIgnoreCase) && Root.Severity == null)
                return null;

            var file = new FileInfo(FileName);
            var parent = file.Directory.Parent;

            while (parent != null)
            {
                string parentFileName = Path.Combine(parent.FullName, Constants.FileName);

                if (File.Exists(parentFileName))
                {
                    var doc = DocumentService.CreateAndLoadTextDocument(parentFileName, _contentType);
                    return new EditorConfigDocument(doc.TextBuffer) { FileName = parentFileName };
                }

                parent = parent.Parent;
            }

            return null;
        }
    }
}
