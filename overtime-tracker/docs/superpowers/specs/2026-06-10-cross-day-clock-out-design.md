# Cross-Day Clock-Out Review Design

## Scope

This change keeps the product focused on recording time. It adds clearer entry prompts and export columns for records that need review, without changing original records after they are created.

## Clock-In

Clock-in requires two pieces of information:

- Work category: one of design, sales, purchase, bidding, project management, site support, customer communication, internal affairs, or other.
- Reason: free text explaining the concrete work.

The record keeps this in the existing note field using a readable `category: reason` format. Export splits that note into separate work category and reason columns.

## Clock-Out

Clock-out normally requires no explanation. It requires a clock-out explanation when either condition is true:

- The clock-in and clock-out range status differs.
- The clock-out happens on a different date from the matching clock-in.

For location mismatch, the quick choices are:

- Temporary offsite finish
- Returned to office finish
- Location accuracy issue
- Other

For cross-day, the quick choices are:

- Work actually continued to next day
- Forgot to clock out, recording now
- Location or device delay
- Cross-day with finish location change
- Other

The system still records the real clock-out timestamp. It does not rewrite time based on the explanation.

## Export

CSV exports add these columns:

- Work category
- Clock-out explanation
- Review marker

The existing reason column contains only clock-in reasons. Clock-out explanations are separate. Review marker is system-generated with values such as `位置不一致`, `跨天`, or `位置不一致；跨天`.

## Validation

Frontend prompts guide normal use. Backend validation enforces the same rules so records cannot bypass required explanations.
