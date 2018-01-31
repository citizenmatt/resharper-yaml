using JetBrains.Application.Settings;
using JetBrains.ReSharper.Daemon.Stages;
using JetBrains.ReSharper.Daemon.UsageChecking;
using JetBrains.ReSharper.Feature.Services.Daemon;
using JetBrains.ReSharper.Plugins.Yaml.Daemon.Errors;
using JetBrains.ReSharper.Plugins.Yaml.Psi;
using JetBrains.ReSharper.Plugins.Yaml.Psi.Tree;
using JetBrains.ReSharper.Psi;
using JetBrains.ReSharper.Psi.Tree;

namespace JetBrains.ReSharper.Plugins.Yaml.Daemon.Stages
{
  [DaemonStage(StagesBefore = new[] {typeof(GlobalFileStructureCollectorStage)},
    StagesAfter = new[] {typeof(CollectUsagesStage)})]
  public class YamlSyntaxErrorHighlightStage : YamlDaemonStageBase
  {
    protected override IDaemonStageProcess CreateProcess(IDaemonProcess process, IContextBoundSettingsStore settings,
      DaemonProcessKind processKind, IYamlFile file)
    {
      return new YamlSyntaxErrorHighlightProcess(process, processKind, file);
    }

    protected override bool IsSupported(IPsiSourceFile sourceFile)
    {
      // Don't check PSI properties - a syntax error is a syntax error
      if (sourceFile == null || !sourceFile.IsValid())
        return false;

      return sourceFile.IsLanguageSupported<YamlLanguage>();
    }

    private class YamlSyntaxErrorHighlightProcess : YamlDaemonStageProcessBase
    {
      public YamlSyntaxErrorHighlightProcess(IDaemonProcess process, DaemonProcessKind processKind, IYamlFile file)
        : base(process, file)
      {
      }

      public override void VisitNode(ITreeNode node, IHighlightingConsumer consumer)
      {
        if (node is IErrorElement errorElement)
        {
          var range = errorElement.GetDocumentRange();
          if (!range.IsValid())
            range = node.Parent.GetDocumentRange();
          if (range.TextRange.IsEmpty)
          {
            if (range.TextRange.EndOffset < range.Document.GetTextLength())
              range = range.ExtendRight(1);
            else if (range.TextRange.StartOffset > 0)
              range = range.ExtendLeft(1);
          }
          consumer.AddHighlighting(new YamlSyntaxError(errorElement.ErrorDescription, range));
        }

        base.VisitNode(node, consumer);
      }
    }
  }
}
