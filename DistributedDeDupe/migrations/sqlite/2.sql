create table gdrive
(
	id integer not null
		constraint gdrive_pk
			primary key autoincrement,
	filename text,
	fileid text,
	directory integer 
);

