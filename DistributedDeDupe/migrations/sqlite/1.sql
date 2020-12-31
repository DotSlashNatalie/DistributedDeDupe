create table settings
(
	id integer not null
		constraint settings_pk
			primary key autoincrement,
	key varchar(255),
	value varchar(255)
);

create index settings_key_index
	on settings (key);

