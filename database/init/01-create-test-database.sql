-- Runs once, when the PostgreSQL volume is first created.
-- helpdesk_test is used only by the integration and recovery tests, which may change its data.
CREATE DATABASE helpdesk_test;
