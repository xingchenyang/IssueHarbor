CREATE TABLE schema_migrations (
    version INTEGER NOT NULL PRIMARY KEY CHECK (version > 0),
    name TEXT NOT NULL UNIQUE,
    sha256 TEXT NOT NULL CHECK (
        length(sha256) = 64
        AND sha256 NOT GLOB '*[^0-9a-f]*'
    ),
    applied_at_utc TEXT NOT NULL CHECK (
        length(applied_at_utc) = 24
        AND substr(applied_at_utc, 11, 1) = 'T'
        AND substr(applied_at_utc, 24, 1) = 'Z'
    )
);

CREATE TABLE issues (
    issue_id INTEGER NOT NULL PRIMARY KEY,
    first_seen_at_utc TEXT NOT NULL CHECK (
        length(first_seen_at_utc) = 24
        AND substr(first_seen_at_utc, 11, 1) = 'T'
        AND substr(first_seen_at_utc, 24, 1) = 'Z'
    )
);

CREATE TABLE requests (
    request_id TEXT NOT NULL PRIMARY KEY CHECK (
        length(request_id) = 36
        AND request_id = lower(request_id)
        AND substr(request_id, 9, 1) = '-'
        AND substr(request_id, 14, 1) = '-'
        AND substr(request_id, 19, 1) = '-'
        AND substr(request_id, 24, 1) = '-'
        AND substr(request_id, 15, 1) = '7'
        AND substr(request_id, 20, 1) IN ('8', '9', 'a', 'b')
        AND length(replace(request_id, '-', '')) = 32
        AND request_id NOT GLOB '*[^0-9a-f-]*'
    ),
    status TEXT NOT NULL CHECK (status IN ('running', 'completed', 'stopped')),
    stop_reason TEXT CHECK (
        stop_reason IS NULL
        OR stop_reason IN (
            'operator_stopped',
            'execution_deadline',
            'provider_authentication_required',
            'provider_quota_exhausted'
        )
    ),
    created_at_utc TEXT NOT NULL CHECK (
        length(created_at_utc) = 24
        AND substr(created_at_utc, 11, 1) = 'T'
        AND substr(created_at_utc, 24, 1) = 'Z'
    ),
    finished_at_utc TEXT CHECK (
        finished_at_utc IS NULL
        OR (
            length(finished_at_utc) = 24
            AND substr(finished_at_utc, 11, 1) = 'T'
            AND substr(finished_at_utc, 24, 1) = 'Z'
        )
    ),
    CHECK (
        (status = 'stopped' AND stop_reason IS NOT NULL)
        OR (status IN ('running', 'completed') AND stop_reason IS NULL)
    )
);

CREATE TABLE request_issues (
    request_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL CHECK (ordinal >= 0),
    issue_id INTEGER NOT NULL,
    PRIMARY KEY (request_id, issue_id),
    UNIQUE (request_id, ordinal),
    FOREIGN KEY (request_id) REFERENCES requests (request_id) ON DELETE RESTRICT,
    FOREIGN KEY (issue_id) REFERENCES issues (issue_id) ON DELETE RESTRICT
);

CREATE TABLE snapshots (
    snapshot_id TEXT NOT NULL PRIMARY KEY CHECK (
        length(snapshot_id) = 36
        AND snapshot_id = lower(snapshot_id)
        AND substr(snapshot_id, 9, 1) = '-'
        AND substr(snapshot_id, 14, 1) = '-'
        AND substr(snapshot_id, 19, 1) = '-'
        AND substr(snapshot_id, 24, 1) = '-'
        AND substr(snapshot_id, 15, 1) = '7'
        AND substr(snapshot_id, 20, 1) IN ('8', '9', 'a', 'b')
        AND length(replace(snapshot_id, '-', '')) = 32
        AND snapshot_id NOT GLOB '*[^0-9a-f-]*'
    ),
    issue_id INTEGER NOT NULL,
    created_at_utc TEXT NOT NULL CHECK (
        length(created_at_utc) = 24
        AND substr(created_at_utc, 11, 1) = 'T'
        AND substr(created_at_utc, 24, 1) = 'Z'
    ),
    artifact_relative_path TEXT NOT NULL CHECK (
        length(artifact_relative_path) > 0
        AND substr(artifact_relative_path, 1, 1) NOT IN ('/', char(92))
        AND artifact_relative_path NOT GLOB '[A-Za-z]:*'
        AND instr(artifact_relative_path, '..') = 0
    ),
    artifact_sha256 TEXT NOT NULL CHECK (
        length(artifact_sha256) = 64
        AND artifact_sha256 NOT GLOB '*[^0-9a-f]*'
    ),
    artifact_size_bytes INTEGER NOT NULL CHECK (artifact_size_bytes > 0),
    UNIQUE (snapshot_id, issue_id),
    FOREIGN KEY (issue_id) REFERENCES issues (issue_id) ON DELETE RESTRICT
);

CREATE TABLE request_items (
    request_item_id TEXT NOT NULL PRIMARY KEY CHECK (
        length(request_item_id) = 36
        AND request_item_id = lower(request_item_id)
        AND substr(request_item_id, 9, 1) = '-'
        AND substr(request_item_id, 14, 1) = '-'
        AND substr(request_item_id, 19, 1) = '-'
        AND substr(request_item_id, 24, 1) = '-'
        AND substr(request_item_id, 15, 1) = '7'
        AND substr(request_item_id, 20, 1) IN ('8', '9', 'a', 'b')
        AND length(replace(request_item_id, '-', '')) = 32
        AND request_item_id NOT GLOB '*[^0-9a-f-]*'
    ),
    request_id TEXT NOT NULL,
    issue_id INTEGER NOT NULL,
    profile_key TEXT NOT NULL CHECK (length(profile_key) > 0),
    profile_ordinal INTEGER NOT NULL CHECK (profile_ordinal >= 0),
    status TEXT NOT NULL CHECK (
        status IN ('planned', 'running', 'completed', 'failed', 'blocked', 'not_started')
    ),
    reason_code TEXT,
    snapshot_id TEXT,
    UNIQUE (request_id, issue_id, profile_key),
    UNIQUE (request_item_id, snapshot_id),
    FOREIGN KEY (request_id, issue_id)
        REFERENCES request_issues (request_id, issue_id) ON DELETE RESTRICT,
    FOREIGN KEY (snapshot_id, issue_id)
        REFERENCES snapshots (snapshot_id, issue_id) ON DELETE RESTRICT
);

CREATE TABLE runs (
    run_id TEXT NOT NULL PRIMARY KEY CHECK (
        length(run_id) = 36
        AND run_id = lower(run_id)
        AND substr(run_id, 9, 1) = '-'
        AND substr(run_id, 14, 1) = '-'
        AND substr(run_id, 19, 1) = '-'
        AND substr(run_id, 24, 1) = '-'
        AND substr(run_id, 15, 1) = '7'
        AND substr(run_id, 20, 1) IN ('8', '9', 'a', 'b')
        AND length(replace(run_id, '-', '')) = 32
        AND run_id NOT GLOB '*[^0-9a-f-]*'
    ),
    request_item_id TEXT NOT NULL UNIQUE,
    snapshot_id TEXT NOT NULL,
    started_at_utc TEXT NOT NULL CHECK (
        length(started_at_utc) = 24
        AND substr(started_at_utc, 11, 1) = 'T'
        AND substr(started_at_utc, 24, 1) = 'Z'
    ),
    finished_at_utc TEXT NOT NULL CHECK (
        length(finished_at_utc) = 24
        AND substr(finished_at_utc, 11, 1) = 'T'
        AND substr(finished_at_utc, 24, 1) = 'Z'
    ),
    result_schema_version INTEGER NOT NULL CHECK (result_schema_version = 1),
    change_type TEXT NOT NULL CHECK (
        change_type IN (
            'bug_fix',
            'enhancement',
            'configuration_or_usage',
            'clarification_needed',
            'no_change',
            'unknown'
        )
    ),
    implementation_complexity TEXT NOT NULL CHECK (
        implementation_complexity IN ('none', 'low', 'medium', 'high', 'unknown')
    ),
    requires_code_change TEXT NOT NULL CHECK (
        requires_code_change IN ('yes', 'no', 'uncertain')
    ),
    confidence TEXT NOT NULL CHECK (confidence IN ('low', 'medium', 'high')),
    complexity_rationale TEXT NOT NULL CHECK (length(complexity_rationale) > 0),
    result_relative_path TEXT NOT NULL CHECK (
        length(result_relative_path) > 0
        AND substr(result_relative_path, 1, 1) NOT IN ('/', char(92))
        AND result_relative_path NOT GLOB '[A-Za-z]:*'
        AND instr(result_relative_path, '..') = 0
    ),
    result_sha256 TEXT NOT NULL CHECK (
        length(result_sha256) = 64
        AND result_sha256 NOT GLOB '*[^0-9a-f]*'
    ),
    result_size_bytes INTEGER NOT NULL CHECK (result_size_bytes > 0),
    report_relative_path TEXT NOT NULL CHECK (
        length(report_relative_path) > 0
        AND substr(report_relative_path, 1, 1) NOT IN ('/', char(92))
        AND report_relative_path NOT GLOB '[A-Za-z]:*'
        AND instr(report_relative_path, '..') = 0
    ),
    report_sha256 TEXT NOT NULL CHECK (
        length(report_sha256) = 64
        AND report_sha256 NOT GLOB '*[^0-9a-f]*'
    ),
    report_size_bytes INTEGER NOT NULL CHECK (report_size_bytes > 0),
    CHECK (
        (requires_code_change = 'no' AND implementation_complexity = 'none')
        OR (
            requires_code_change = 'yes'
            AND implementation_complexity IN ('low', 'medium', 'high', 'unknown')
        )
        OR (requires_code_change = 'uncertain' AND implementation_complexity = 'unknown')
    ),
    CHECK (
        change_type NOT IN ('configuration_or_usage', 'no_change')
        OR (requires_code_change = 'no' AND implementation_complexity = 'none')
    ),
    FOREIGN KEY (request_item_id, snapshot_id)
        REFERENCES request_items (request_item_id, snapshot_id) ON DELETE RESTRICT
);
