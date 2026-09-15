-- Narrows wave_issuances_settlement_write_once from `BEFORE UPDATE` to
-- `BEFORE UPDATE OF settled_at, settlement`.
--
-- 0003 declared it with no column list, so every UPDATE of every column on
-- this table enters plpgsql to compare four values the statement could not
-- have touched. The guard itself was never wrong - it returns NEW unchanged
-- for those statements - it was simply asked a question it had no business
-- being asked, on a table the sweep at Task 12 and any future backfill will
-- update by the row.
--
-- WHY THIS CHANGES NO OUTCOME. `UPDATE OF <cols>` fires when a listed column
-- is MENTIONED as a target of the statement, not when its value changes. So
-- every statement that could rewrite a settlement still enters the function
-- - including one that re-sets a column to the value it already holds, which
-- the function still lets through because it compares values (IS DISTINCT
-- FROM) rather than mentions. The only statements that stop entering are the
-- ones that never named a settlement column, and for those the function
-- returned NEW unchanged anyway. test/issuance-schema.test.ts pins both
-- halves: the four settlement transitions keep their outcomes, and the
-- set-to-same-value and settled_at-alone cases are asserted directly.
--
-- BOTH columns, not just `settlement`. A statement naming only settled_at
-- rewrites a settlement too - it moves WHEN the row stopped being live -
-- and satisfies 0003's CHECK ((settled_at IS NULL) = (settlement IS NULL))
-- while doing it, so nothing else on this table would catch it.
--
-- There is no other trigger on wave_issuances and no rule, so a settlement
-- column cannot be written by a statement that does not name it: no BEFORE
-- trigger exists that could assign to NEW behind this one's back.
--
-- The trigger FUNCTION is unchanged - 0003's definition stands. Only the
-- trigger is re-created.
--
-- (This file's name also carries the other half of its task, the waveId
-- sanity bound. That half is a parse-layer change in
-- services/api/src/routes/wave.ts, not SQL, and there is deliberately no
-- CHECK constraint here for it: the authority on which waves exist is the
-- content bundle, not the schema.)

-- DROP first, matching 0003's own idiom for this exact statement, so the
-- file is re-runnable.
DROP TRIGGER IF EXISTS wave_issuances_settlement_write_once ON wave_issuances;
CREATE TRIGGER wave_issuances_settlement_write_once
  BEFORE UPDATE OF settled_at, settlement ON wave_issuances
  FOR EACH ROW
  EXECUTE FUNCTION reject_wave_issuance_settlement_rewrite();
