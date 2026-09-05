import { Button, Checkbox, Group, Modal, Stack, Text } from '@mantine/core';
import { useEffect, useRef, useState } from 'react';
import { setAdministrativeApprovalHandler, type ApiError } from '@/lib/api';

export function AdministrativeApprovalDialog({ onReauthenticate }: { onReauthenticate: () => Promise<void> }) {
  const [request, setRequest] = useState<ApiError | null>(null);
  const [acknowledged, setAcknowledged] = useState(false);
  const resolveRequest = useRef<((approved: boolean) => void) | null>(null);

  useEffect(() => {
    setAdministrativeApprovalHandler((error) => new Promise<boolean>((resolve) => {
      if (resolveRequest.current) { resolve(false); return; }
      resolveRequest.current = resolve;
      setAcknowledged(false);
      setRequest(error);
    }));
    return () => {
      setAdministrativeApprovalHandler(null);
      resolveRequest.current?.(false);
      resolveRequest.current = null;
    };
  }, []);

  function close(approved: boolean) {
    resolveRequest.current?.(approved);
    resolveRequest.current = null;
    setRequest(null);
    setAcknowledged(false);
  }

  const requiresAuthentication = request?.errorCode === 'Forbidden.AdministrativeApproval.ReauthenticationRequired';
  return (
    <Modal opened={request !== null} onClose={() => close(false)} title="Approve administrative access" centered>
      <Stack>
        <Text>This operation changes administrative access. Review the requested permissions. An all-permissions grant includes every current and future platform permission.</Text>
        {requiresAuthentication ? (
          <Text>Sign in again, then repeat this operation to review and approve it.</Text>
        ) : (
          <Checkbox checked={acknowledged} onChange={(event) => setAcknowledged(event.currentTarget.checked)}
            label="I acknowledge the administrative access this operation grants." />
        )}
        <Group justify="flex-end">
          <Button variant="default" onClick={() => close(false)}>Cancel</Button>
          {requiresAuthentication ? (
            <Button onClick={() => { close(false); void onReauthenticate(); }}>Sign in again</Button>
          ) : (
            <Button disabled={!acknowledged} onClick={() => close(true)}>Approve operation</Button>
          )}
        </Group>
      </Stack>
    </Modal>
  );
}
