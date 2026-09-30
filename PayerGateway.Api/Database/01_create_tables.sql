-- =====================================================================================================
-- Payer Gateway API - Oracle tables
--
-- Run once in the schema used by ConnectionStrings:Oracle, then set  "Storage": { "Provider": "Oracle" }.
-- Each table belongs to one workflow step. Lists and decisions are kept as JSON in CLOB columns
-- (checked with IS JSON); the columns you search or report on are real columns.
-- PAYER_NAME = the payer name the EHR sends (appsettings Payers:PayerName).
-- Syntax: Oracle 19c or later.
-- =====================================================================================================

-- ------------------------------------------------------------------ STEP 1: CRD (order-sign)
-- One row per order-sign event: the answer we returned (a repeated eventId gets the same answer, no second payer call).
CREATE TABLE PA_CRD_EVENT (
    EVENT_ID       VARCHAR2(100)  NOT NULL,
    PATIENT_ID     VARCHAR2(64)   NOT NULL,
    PAYER_NAME     VARCHAR2(64)   NOT NULL,
    RESPONSE_JSON  CLOB           NOT NULL,     -- OrderSignResponse
    CREATED_AT     TIMESTAMP      NOT NULL,     -- UTC
    CONSTRAINT PK_PA_CRD_EVENT PRIMARY KEY (EVENT_ID),
    CONSTRAINT CK_PA_CRD_EVENT_JSON CHECK (RESPONSE_JSON IS JSON)
);

-- Latest coverage decision per order (GET /api/v1/crd/decisions/{orderId}).
CREATE TABLE PA_COVERAGE_DECISION (
    ORDER_ID               VARCHAR2(64)  NOT NULL,
    EVENT_ID               VARCHAR2(100) NOT NULL,
    PAYER_NAME             VARCHAR2(64),
    PATIENT_ID             VARCHAR2(64)  NOT NULL,
    COVERAGE_ASSERTION_ID  VARCHAR2(100),            -- payer's id for this answer (DTR "context")
    COVERED                VARCHAR2(20)  NOT NULL,   -- covered | not-covered | conditional | unknown
    PA_NEEDED              VARCHAR2(20),             -- no-auth | auth-needed | satisfied | performpa | conditional
    DOC_NEEDED             VARCHAR2(20),             -- no-doc | clinical | admin | both | conditional
    DECISION_JSON          CLOB          NOT NULL,   -- CoverageDecision
    UPDATED_AT             TIMESTAMP     NOT NULL,
    CONSTRAINT PK_PA_COVERAGE_DECISION PRIMARY KEY (ORDER_ID),
    CONSTRAINT CK_PA_COVERAGE_DECISION_JSON CHECK (DECISION_JSON IS JSON)
);
CREATE INDEX IX_PA_COVERAGE_DECISION_PAT ON PA_COVERAGE_DECISION (PATIENT_ID);

-- ------------------------------------------------------------------ STEP 2: DTR
-- Payer questionnaires fetched for orders (POST /api/v1/dtr/forms) and their latest plain answers.
-- The filled-in FHIR QuestionnaireResponse itself is stored by the Provider FHIR API.
CREATE TABLE PA_DTR_FORM (
    FORM_ID                    VARCHAR2(64)   NOT NULL,
    PAYER_NAME                 VARCHAR2(64)   NOT NULL,
    PATIENT_ID                 VARCHAR2(64)   NOT NULL,
    COVERAGE_ID                VARCHAR2(64),
    ORDER_IDS_JSON             CLOB,                      -- ["ord-1001"]
    QUESTIONNAIRE_URL          VARCHAR2(1000) NOT NULL,
    QUESTIONNAIRE_JSON         CLOB           NOT NULL,   -- the payer Questionnaire
    STATUS                     VARCHAR2(20)   NOT NULL,   -- new | in-progress | completed
    QUESTIONNAIRE_RESPONSE_ID  VARCHAR2(64),              -- id at the Provider FHIR API
    ANSWERS_JSON               CLOB,                      -- [{"linkId":"egfr","values":["12"]}]
    ADAPTIVE                   NUMBER(1)      DEFAULT 0 NOT NULL,   -- 1 = payer sends questions with $next-question
    PAYER_FINISHED             NUMBER(1)      DEFAULT 0 NOT NULL,   -- adaptive: 1 = payer has no more questions
    ADAPTIVE_RESPONSE_JSON     CLOB,                      -- adaptive: last QuestionnaireResponse from the payer
    CREATED_AT                 TIMESTAMP      NOT NULL,
    UPDATED_AT                 TIMESTAMP      NOT NULL,
    CONSTRAINT PK_PA_DTR_FORM PRIMARY KEY (FORM_ID),
    CONSTRAINT CK_PA_DTR_FORM_JSON CHECK (QUESTIONNAIRE_JSON IS JSON)
);
CREATE INDEX IX_PA_DTR_FORM_PAT ON PA_DTR_FORM (PATIENT_ID);

-- Payer SMART DTR app links written to the EHR (future: when payers bring their own SMART apps).
CREATE TABLE PA_DTR_LINK (
    LINK_ID               VARCHAR2(64)   NOT NULL,
    PAYER_NAME            VARCHAR2(64)   NOT NULL,
    PATIENT_ID            VARCHAR2(64)   NOT NULL,
    ENCOUNTER_ID          VARCHAR2(64),
    USER_PRACTITIONER_ID  VARCHAR2(64),
    FHIR_CONTEXT_JSON     CLOB,                       -- ["ServiceRequest/ord-1001","Coverage/cov-1001"]
    APP_CONTEXT           CLOB,                       -- appContext from the payer's card
    APP_URL               VARCHAR2(1000) NOT NULL,
    EXPIRES_AT            TIMESTAMP      NOT NULL,
    CONSTRAINT PK_PA_DTR_LINK PRIMARY KEY (LINK_ID)
);

-- ------------------------------------------------------------------ STEP 3: PAS
-- One row per prior authorization request (updated on every update / cancel / inquiry).
CREATE TABLE PA_PRIOR_AUTH (
    REQUEST_ID            VARCHAR2(64)  NOT NULL,     -- EHR request id = Claim.identifier
    PAYER_NAME            VARCHAR2(64)  NOT NULL,
    PATIENT_ID            VARCHAR2(64)  NOT NULL,
    COVERAGE_ID           VARCHAR2(64)  NOT NULL,
    MEMBER_ID             VARCHAR2(64),
    ORGANIZATION_ID       VARCHAR2(64)  NOT NULL,
    ORGANIZATION_NPI      VARCHAR2(20),
    PRACTITIONER_ID       VARCHAR2(64),
    PRACTITIONER_NPI      VARCHAR2(20),
    PRIORITY              VARCHAR2(10)  NOT NULL,     -- routine | urgent
    STATUS                VARCHAR2(20)  NOT NULL,     -- approved | partially-approved | denied | pended | not-required | cancelled | contact-payer | error
    AUTHORIZATION_NUMBER  VARCHAR2(64),
    VERSION_NO            NUMBER(5)     NOT NULL,     -- 1 = first submission, +1 per update / cancel
    ORDER_IDS_JSON        CLOB,                       -- ["ord-1001"]
    LINE_NUMBERS_JSON     CLOB,                       -- {"ord-1001":1}
    DECISION_JSON         CLOB,                       -- PriorAuthDecision (what the EHR receives)
    LAST_REQUEST_BUNDLE   CLOB,                       -- last PAS Bundle sent (needed for $inquire)
    LAST_RESPONSE_BUNDLE  CLOB,                       -- last payer response (support)
    EHR_SYNC_STATUS       VARCHAR2(10)  NOT NULL,     -- SENT | FAILED (FAILED is retried by the background job)
    SUBMITTED_AT          TIMESTAMP     NOT NULL,
    UPDATED_AT            TIMESTAMP     NOT NULL,
    CONSTRAINT PK_PA_PRIOR_AUTH PRIMARY KEY (REQUEST_ID),
    CONSTRAINT CK_PA_PRIOR_AUTH_DECISION CHECK (DECISION_JSON IS JSON)
);
CREATE INDEX IX_PA_PRIOR_AUTH_STATUS ON PA_PRIOR_AUTH (STATUS, PAYER_NAME);
CREATE INDEX IX_PA_PRIOR_AUTH_SYNC   ON PA_PRIOR_AUTH (EHR_SYNC_STATUS);
CREATE INDEX IX_PA_PRIOR_AUTH_PAT    ON PA_PRIOR_AUTH (PATIENT_ID);

-- PAS Subscriptions created at payers (one per payer + provider organization NPI).
CREATE TABLE PA_PAYER_SUBSCRIPTION (
    PAYER_NAME        VARCHAR2(64)  NOT NULL,
    ORGANIZATION_NPI  VARCHAR2(20)  NOT NULL,
    SUBSCRIPTION_ID   VARCHAR2(100),
    STATUS            VARCHAR2(10)  NOT NULL,         -- active | error
    CREATED_AT        TIMESTAMP     NOT NULL,
    CONSTRAINT PK_PA_PAYER_SUBSCRIPTION PRIMARY KEY (PAYER_NAME, ORGANIZATION_NPI)
);

-- ------------------------------------------------------------------ STEP 4: CDex
-- Payer information requests (Tasks) received for our prior auth requests.
CREATE TABLE PA_CDEX_TASK (
    PAYER_NAME       VARCHAR2(64)   NOT NULL,
    TASK_ID          VARCHAR2(100)  NOT NULL,
    REQUEST_ID       VARCHAR2(64)   NOT NULL,
    TRACKING_ID      VARCHAR2(100)  NOT NULL,         -- sent back in $submit-attachment
    TRACKING_SYSTEM  VARCHAR2(500),
    STATUS           VARCHAR2(20),                    -- requested | in-progress | completed | cancelled ...
    DUE_DATE         VARCHAR2(30),
    TASK_JSON        CLOB           NOT NULL,         -- the Task as received
    RECEIVED_AT      TIMESTAMP      NOT NULL,
    CONSTRAINT PK_PA_CDEX_TASK PRIMARY KEY (PAYER_NAME, TASK_ID),
    CONSTRAINT CK_PA_CDEX_TASK_JSON CHECK (TASK_JSON IS JSON)
);
CREATE INDEX IX_PA_CDEX_TASK_REQUEST ON PA_CDEX_TASK (REQUEST_ID);

-- ------------------------------------------------------------------ useful queries
-- Open prior auths:                SELECT REQUEST_ID, PAYER_NAME, SUBMITTED_AT FROM PA_PRIOR_AUTH WHERE STATUS = 'pended';
-- Decisions not yet in the EHR:    SELECT REQUEST_ID FROM PA_PRIOR_AUTH WHERE EHR_SYNC_STATUS = 'FAILED';
-- Forms not finished:              SELECT FORM_ID, PATIENT_ID, QUESTIONNAIRE_URL FROM PA_DTR_FORM WHERE STATUS <> 'completed';
