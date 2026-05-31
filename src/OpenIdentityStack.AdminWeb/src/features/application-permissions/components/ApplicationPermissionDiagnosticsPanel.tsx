import { AlertTriangle, RefreshCw } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { useApplicationPermissionDiagnostics } from '../hooks';

export function ApplicationPermissionDiagnosticsPanel() {
  const { data, isFetching, refetch } = useApplicationPermissionDiagnostics();
  const issues = data?.issues ?? [];

  if (issues.length === 0) {
    return null;
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between gap-4">
        <div className="space-y-1">
          <CardTitle className="text-lg">Assignment Diagnostics</CardTitle>
          <p className="text-sm text-muted-foreground">
            {issues.length} permission assignment issue{issues.length === 1 ? '' : 's'} {issues.length === 1 ? 'needs' : 'need'} review.
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => void refetch()} disabled={isFetching}>
          <RefreshCw className="mr-2 h-4 w-4" />
          Refresh
        </Button>
      </CardHeader>
      <CardContent>
        <div className="space-y-3">
          {issues.slice(0, 6).map((issue) => (
            <div key={`${issue.assignmentStore}:${issue.assignmentId}:${issue.permission}`} className="rounded-md border p-3">
              <div className="flex flex-wrap items-center gap-2">
                <AlertTriangle className="h-4 w-4 text-amber-600" />
                <code className="rounded bg-muted px-2 py-1 text-xs">{issue.permission}</code>
                <Badge variant="secondary">{issue.kind}</Badge>
              </div>
              <p className="mt-2 text-sm text-muted-foreground">{issue.message}</p>
              <p className="mt-1 text-xs text-muted-foreground">
                {issue.assignmentStore}: {issue.assignmentDisplayName ?? issue.assignmentId}
              </p>
            </div>
          ))}
          {issues.length > 6 ? <p className="text-xs text-muted-foreground">{issues.length - 6} more issues hidden.</p> : null}
        </div>
      </CardContent>
    </Card>
  );
}
