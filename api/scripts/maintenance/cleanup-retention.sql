-- Safe to run repeatedly. Deletes in batches to keep transactions bounded.
DO $$
DECLARE
    batch_size integer := 1000;
    affected integer;
    audit_deleted bigint := 0;
    session_deleted bigint := 0;
BEGIN
    LOOP
        WITH doomed AS (
            SELECT id
            FROM audit_logs
            WHERE created_at < CURRENT_TIMESTAMP - INTERVAL '90 days'
            ORDER BY created_at
            LIMIT batch_size
        )
        DELETE FROM audit_logs target
        USING doomed
        WHERE target.id = doomed.id;

        GET DIAGNOSTICS affected = ROW_COUNT;
        audit_deleted := audit_deleted + affected;
        EXIT WHEN affected < batch_size;
    END LOOP;

    LOOP
        WITH doomed AS (
            SELECT id
            FROM auth_sessions
            WHERE COALESCE(revoked_at, refresh_token_expires_at)
                  < CURRENT_TIMESTAMP - INTERVAL '30 days'
            ORDER BY COALESCE(revoked_at, refresh_token_expires_at)
            LIMIT batch_size
        )
        DELETE FROM auth_sessions target
        USING doomed
        WHERE target.id = doomed.id;

        GET DIAGNOSTICS affected = ROW_COUNT;
        session_deleted := session_deleted + affected;
        EXIT WHEN affected < batch_size;
    END LOOP;

    RAISE NOTICE 'cleanup complete: audit_logs=%, auth_sessions=%', audit_deleted, session_deleted;
END $$;
