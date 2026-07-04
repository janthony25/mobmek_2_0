# 2026-07-04

## Customer Page
- [x] In customer page user should also be able to search using REGO# (done: 2026-07-04 10:30)

## Customer Details Page
- [x] When I click View job in from Invoice/Quotes action button it leads me to the job page but there is no button to go back to that customer details page. Add that. (done: 2026-07-04 14:24)
- [x] In Invoices and Quotes section add number that counts all of the invoices/quotes under that car — implemented as both: total count in "Invoices (n)"/"Quotes (n)" card titles, plus a per-vehicle count badge in the Vehicles list (done: 2026-07-04 14:24)
- [x] In vehicle section instead of adding a logo with number for number of invoices and quotation just add Invoices: and Quotes: under the vehicle details so its more detailed (done: 2026-07-04 14:35)
- [x] Add search functionality for invoices section(calendar icon) so we can easily look for invoice within a date or week or month (done: 2026-07-04 13:05)
- [x] In invoices section right now its not so visible because there is just a gray line that separates them make make the border or the line that separates them more visible (Maybe update our Claude.MD for frontend since we would be having more like this like in Vehicle Details Page there is a job section that has list as well) (done: 2026-07-04 13:05)
- [x] make the invoice section default invoices only shows 5 (done: 2026-07-04 13:50; verified via tsc+lint only, no live browser click-through this session — no browser-automation tool available)
- [x] In invoices section add a dropdown button for the action that is used for invoices page as well, add clear filter as well (done: 2026-07-04 13:50; verified via tsc+lint only, no live browser click-through this session — no browser-automation tool available)
- [x] Add Quotes section under Invoices section with action button as well with 5 as limit and has search calendar as well with clear filter (done: 2026-07-04 13:50; verified via tsc+lint only, no live browser click-through this session — no browser-automation tool available)

## Job Page
- [x] In job page have a card version as well like customer where it has cards and list view. with also maximum of 10 cards (done: 2026-07-04 10:53)
- [x] In Add job page - When there are required fields that was not filled, instead of having a line at the end of the page make sure the page goes to that field with red background for the iput(for visibility) and not that the field is required (done: 2026-07-04 13:35)
- [x] In add job  when adding fields like when adding items/labour the remove button should have red background for visibility (done: 2026-07-04 13:35)
- [x] Add create appointment that would be link to the appointment page (done: 2026-07-04 13:50; verified via tsc+lint only, no live browser click-through this session — no browser-automation tool available)

## Vehicle Details Page
- [x] in reminders section, instead of having multiple action buttons in one line, we should make it a dropdown(...) (done: 2026-07-04 13:50)
- [x] In job section its hard to see different jobs because there are no borders for each job and just pure black text, maybe add border or if you have suggestion for better visibility (done: 2026-07-04 13:50)

## Appointment Page
- [x] In creating appointment, in existing customer. Make the Customer be input where user can type then a dropdown based on what the user is typing would show the list. Currently its just a dropdown of all customer which is not ideal for big database (done: 2026-07-04 11:05)
- [x] when creating an appointment aside from existing customer and new caller, user should be able to create appointment through existing job where it shows job cards and user could search using rego# to show only specific job for that rego (done: 2026-07-04 11:05)
- [x] when creating new appointment, when choosing existing job, only show 6 most recent job created as default and limit so it
would be user friendly if we have too much job and we can just search the rego if needed (done: 2026-07-04 10:57)
- [x] when an appointment is clicked, it should be easier to update the status instead of clicking to edit that has a lot more inputs needed. (done: 2026-07-04 11:49)
- [x] When an appointment is clicked, Vehicle should also be clickeable to be able straight to vehicle details page (done: 2026-07-04 11:49)

## General
- [x] Create pages for Invoices and Quotations. With 20 as default limit and has search functionality that can be name, rego# or date (done: 2026-07-04 12:04)
- [x] In invoices and quotation page, for date search it should be a calendar button so user could choose the date easily instead of typing (done: 2026-07-04 14:10)
- [x] For invoice and quotation add action dropdown button where it would have Mark as paid View Job, View Invoice/PDF (PDF), Mark as Paid (for invoice), Download Invoice/Quote, Send Email (to be implemented soon), Reject (done: 2026-07-04 14:35)
- [x] In Invoices and Quotation page, add a minimal show all button so that when it is filterd its easier to show all (done: 2026-07-04 13:50; verified via tsc+lint only, no live browser click-through this session — no browser-automation tool available)
- [x] In Invoices and Quotation page, when view job is clicked, make sure we have back to "Invoices/Quotes" Page (done: 2026-07-04 13:50; verified via tsc+lint only, no live browser click-through this session — no browser-automation tool available)
- [x] In Invoices and Quotation page, when user click view job in specific page. They should be able to go back to the invoices/quotation page. Easiest way for this is to just go back to previous page when they use this button from invoices/quotation page. (done: 2026-07-04 14:24; confirmed already implemented via back-link state — re-verified live with Playwright from both /invoices and /quotations)
- [x] In customer details page when I click view job from invoices/quotes section Make sure that the Back to previous page is on the very top left not after reminders. Same when user is from Invoices and Quotation Page. Made this a standard in mobmek_frontend/CLAUDE.md that all back links go at the very top of a detail page's JSX. (done: 2026-07-04 14:35)
- [x] We need to add loading for page changes, search and other(check system and analyze what else need more loading) cause once its deployed it will be hard to know if its loading or there is really no data depending on internet speed or how big the data is. (done: 2026-07-04 15:11; root-caused and fixed a real bug in useAsync's reload() that could flash "Customer/Job/Car not found" or empty-list states after any save/delete/action across the app; added a shared Spinner component, inline refresh spinners on CrudSection/Invoices/Quotations/Appointments, a search-box spinner, and busy/disabled state on per-row actions; verified via tsc+lint and a live Playwright pass — no console errors, no stale/not-found flash across 10 rapid post-save snapshots)
